using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Gedcom.Extensions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.UI.HtmlExport;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Genealogy;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Runs HtmlExporter against a real on-disk project filled through the real GEDCOM importer, then reads
/// the zip it produced back: the pages only mean anything alongside the entries their links point at.
/// </summary>
public sealed partial class HtmlExporterTests : IAsyncLifetime
{
  // The couple's marriage record is a FAM OBJE, which import attaches to both spouses -- the one piece of
  // media this project owns twice. Its name carries a space so the links have something to escape.
  private const string Gedcom = """
    0 HEAD
    1 CHAR UTF-8
    0 @I1@ INDI
    1 NAME John /Smith/
    1 SEX M
    1 BIRT
    2 DATE 1 JAN 1900
    1 DEAT
    2 DATE 1970
    1 FAMS @F1@
    1 OBJE
    2 FILE portrait.png
    3 FORM png
    0 @I2@ INDI
    1 NAME Mary /Smith/
    1 SEX F
    1 FAMS @F1@
    0 @I3@ INDI
    1 NAME Tom /Smith/
    1 SEX M
    1 FAMC @F1@
    0 @I4@ INDI
    1 NAME Solo
    1 SEX U
    0 @F1@ FAM
    1 HUSB @I1@
    1 WIFE @I2@
    1 CHIL @I3@
    1 MARR
    2 DATE 1925
    1 OBJE
    2 FILE marriage record.pdf
    3 FORM pdf
    0 TRLR
    """;

  // Three generations for the main-person page: Tom's grandfather Old holds the only photo, so it shows
  // only if an expanded row still gets one.
  private const string LineageGedcom = """
    0 HEAD
    1 CHAR UTF-8
    0 @I1@ INDI
    1 NAME Old /Smith/
    1 SEX M
    1 FAMS @F0@
    1 OBJE
    2 FILE portrait.png
    3 FORM png
    0 @I2@ INDI
    1 NAME John /Smith/
    1 SEX M
    1 FAMC @F0@
    1 FAMS @F1@
    0 @I3@ INDI
    1 NAME Mary /Smith/
    1 SEX F
    1 FAMS @F1@
    0 @I4@ INDI
    1 NAME Tom /Smith/
    1 SEX M
    1 FAMC @F1@
    0 @F0@ FAM
    1 HUSB @I1@
    1 CHIL @I2@
    0 @F1@ FAM
    1 HUSB @I2@
    1 WIFE @I3@
    1 CHIL @I4@
    0 TRLR
    """;

  private const string CaptionedGedcom = """
    0 HEAD
    1 CHAR UTF-8
    0 @I1@ INDI
    1 NAME John /Smith/
    1 SEX M
    1 OBJE
    2 FILE portrait.png
    3 FORM png
    2 TITL John at the mill
    0 TRLR
    """;

  private readonly string _Folder = Path.Combine(Path.GetTempPath(), $"gt4_html_{Guid.NewGuid():N}");
  private readonly List<IProjectDocument> _Documents = [];
  private IProjectDocument _Document = null!;

  private static CancellationToken Token => TestContext.Current.CancellationToken;

  public async ValueTask InitializeAsync()
  {
    Directory.CreateDirectory(_Folder);
    var portrait = PngHeader(640, 480);
    var portraitPath = Path.Combine(_Folder, "portrait.png");
    var recordPath = Path.Combine(_Folder, "marriage record.pdf");
    await File.WriteAllBytesAsync(portraitPath, portrait, Token);
    await File.WriteAllBytesAsync(recordPath, [0x25, 0x50, 0x44, 0x46], Token);
    _Document = await ImportAsync(Gedcom);
  }

  public async ValueTask DisposeAsync()
  {
    foreach (var document in _Documents)
    {
      await document.DisposeAsync();
    }
    try { Directory.Delete(_Folder, recursive: true); } catch { /* best-effort temp cleanup */ }
  }

  private async Task<IProjectDocument> ImportAsync(string gedcom)
  {
    var factory = new TestServices().Provider.GetRequiredService<IProjectDocumentFactory>();
    var projectPath = Path.Combine(_Folder, $"project{_Documents.Count}.gt4");
    var document = await factory.CreateNewAsync(projectPath, "Smiths", Token);
    _Documents.Add(document);
    var importer = new ServiceCollection().AddGedcom().BuildServiceProvider().GetRequiredService<IGedcomImporter>();
    using var reader = new StringReader(gedcom);
    await importer.ImportAsync(document, reader, Token, _Folder);
    return document;
  }

  // Just the signature and the IHDR dimensions, which is all the exporter reads to size an inline image.
  private static byte[] PngHeader(int width, int height)
  {
    var png = new byte[24];
    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
    "IHDR"u8.CopyTo(png.AsSpan(12));
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), (uint)width);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), (uint)height);
    return png;
  }

  private async Task<PersonFullInfo> PersonAsync(string givenName, IProjectDocument? document = null)
  {
    document ??= _Document;
    var persons = await document.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, Token);
    var person = persons.Single(p => p.Names.Any(name => name.Value == givenName));
    return await document.PersonManager.GetPersonFullInfoAsync(person, Token);
  }

  private async Task SetBiographyAsync(PersonFullInfo person, string markdown)
  {
    var content = Encoding.UTF8.GetBytes(markdown);
    var biography = new Data(ElementId.NonCommittedId, content, "text/plain", DataCategory.PersonBio);
    await _Document.PersonManager.UpdatePersonAsync(person with { Biography = biography }, Token);
  }

  private async Task<Site> ExportAsync(
    CancellationToken token = default,
    string projectName = "Smiths",
    IProjectDocument? document = null,
    MainPersonExport? mainPerson = null)
  {
    var exporter = new TestServices().Provider.GetRequiredService<HtmlExporter>();
    using var output = new MemoryStream();
    await exporter.ExportAsync(document ?? _Document, projectName, mainPerson, output, token);

    output.Position = 0;
    using var archive = new ZipArchive(output, ZipArchiveMode.Read);
    var entries = new List<(string Name, byte[] Content)>();
    foreach (var entry in archive.Entries)
    {
      using var stream = entry.Open();
      using var copy = new MemoryStream();
      await stream.CopyToAsync(copy, Token);
      entries.Add((entry.FullName, copy.ToArray()));
    }
    return new Site(entries);
  }

  private sealed class Site(List<(string Name, byte[] Content)> entries)
  {
    public string[] Names => [.. entries.Select(entry => entry.Name)];

    public byte[] Content(string name) => entries.Single(entry => entry.Name == name).Content;

    public string Page(string name)
    {
      var page = entries.Single(entry => entry.Name == name);
      return Encoding.UTF8.GetString(page.Content);
    }

    public string PersonPage(Person person) => Page($"person-{person.Id}.html");

    public IEnumerable<string> Pages => entries.Where(entry => entry.Name.EndsWith(".html")).Select(entry => entry.Name);

    // The photo viewer opens in a window of its own, so it carries no site header.
    public IEnumerable<string> HeaderedPages => Pages.Where(name => name != "photo.html");
  }

  // A card is the link to that person's page, up to where its anchor closes.
  private static string Card(string page, Person person)
  {
    var start = page.IndexOf($"<a class=\"card\" href=\"person-{person.Id}.html\"");
    Assert.True(start >= 0, $"No card for person {person.Id}.");
    var end = page.IndexOf("</a>", start);
    return page[start..end];
  }

  private static string Heading(string text) => $"<h2>{System.Net.WebUtility.HtmlEncode(text)}</h2>";

  // As a page writes it: the fragment's values escaped, then the whole href HTML-encoded.
  private static string ViewerHref(Data photo, string caption = "")
  {
    var escapedCaption = Uri.EscapeDataString(caption);
    return $"photo.html#src=media%2F{photo.Id}%2Fphoto.png&amp;caption={escapedCaption}";
  }

  private static string FileOf(string link)
  {
    var path = link.Split('#')[0];
    return Uri.UnescapeDataString(path);
  }

  // The viewer's fragment holds the photo's own href, escaped once more.
  private static string ViewedPhotoOf(string link)
  {
    var fragment = link.Split('#')[1];
    var decoded = System.Net.WebUtility.HtmlDecode(fragment);
    var values = System.Web.HttpUtility.ParseQueryString(decoded);
    return Uri.UnescapeDataString(values["src"]!);
  }

  [GeneratedRegex("(?:href|src)=\"([^\"]*)\"")]
  private static partial Regex LinkPattern();

  [GeneratedRegex("<section class=\"initial\" id=\"[^\"]*\">\n<h3>([^<]*)</h3>(.*?)</section>", RegexOptions.Singleline)]
  private static partial Regex InitialGroupPattern();

  [GeneratedRegex("<a class=\"card\"")]
  private static partial Regex CardPattern();

  [GeneratedRegex("<a class=\"tree-node(?: main)?\" href=\"person-(\\d+)\\.html\" style=\"left:([0-9.]+)px")]
  private static partial Regex TreeNodePattern();

  // Root and eight generations of two children each, 511 persons numbered as a binary heap (Pn's children
  // are P2n and P2n+1): more than the relatives list or the tree holds. One wide family would instead list
  // all its children on each of their pages, enough to exhaust the test host.
  private static string DescendantsGedcom()
  {
    const int count = 511;
    const int parents = count / 2;
    var persons = Enumerable.Range(1, count).Select(n =>
    {
      var name = n == 1 ? "Root" : $"P{n}";
      var childOf = n == 1 ? string.Empty : $"\n1 FAMC @F{n / 2}@";
      var parentIn = n > parents ? string.Empty : $"\n1 FAMS @F{n}@";
      return $"0 @P{n}@ INDI\n1 NAME {name} /Heap/\n1 SEX M{childOf}{parentIn}";
    });
    var families = Enumerable.Range(1, parents).Select(n => $"0 @F{n}@ FAM\n1 HUSB @P{n}@\n1 CHIL @P{2 * n}@\n1 CHIL @P{(2 * n) + 1}@");
    string[] lines = ["0 HEAD", "1 CHAR UTF-8", .. persons, .. families, "0 TRLR"];
    return string.Join('\n', lines);
  }

  private static MainPersonExport MainPerson(PersonInfo person, int[]? hiddenIds = null, Dictionary<int, double>? pins = null) =>
    new(person, hiddenIds ?? [], pins ?? []);

  // A relatives-list row, from its item to where its card closes.
  private static string Row(string page, Person person)
  {
    var card = page.IndexOf($"<a class=\"card\" href=\"person-{person.Id}.html\"");
    Assert.True(card >= 0, $"No row for person {person.Id}.");
    var start = page.LastIndexOf("<li", card);
    var end = page.IndexOf("</a>", card);
    return page[start..end];
  }

  private static Dictionary<int, double> TreeNodeLefts(string page)
  {
    var matches = TreeNodePattern().Matches(page);
    return matches.ToDictionary(
      match => int.Parse(match.Groups[1].Value),
      match => double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
  }

  [Fact]
  public async Task EveryLinkResolvesToAnEntryInTheArchive()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var photo = john.MainPhoto!;
    var attachment = john.Attachments.Single();
    await SetBiographyAsync(john, $"Married [Mary](person:{mary.Id}).\n\n![Portrait](media:{photo.Id})\n\n[Record](attachment:{attachment.Id})");

    var site = await ExportAsync();

    var links = new List<string>();
    foreach (var page in site.Pages.Select(site.Page))
    {
      var matches = LinkPattern().Matches(page);
      string[] pageLinks = [.. matches.Select(match => match.Groups[1].Value)];
      var fragments = pageLinks.Where(link => link.StartsWith('#'));
      var targets = pageLinks.Except(fragments).Select(FileOf);
      var viewed = pageLinks.Where(link => link.StartsWith("photo.html#")).Select(ViewedPhotoOf);
      Assert.All(fragments, fragment => Assert.Contains($"id=\"{fragment[1..]}\"", page));
      Assert.All(targets, target => Assert.Contains(target, site.Names));
      Assert.All(viewed, target => Assert.Contains(target, site.Names));
      links.AddRange(pageLinks);
    }
    Assert.Contains(links, link => link.StartsWith("media/"));
    Assert.Contains(links, link => link.StartsWith('#'));
    Assert.Contains(links, link => link.StartsWith("photo.html#"));
  }

  [Fact]
  public async Task MediaOwnedByBothSpouses_IsWrittenOnceAndLinkedFromBoth()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var shared = john.Attachments.Single();
    var marys = mary.Attachments.Single();
    Assert.Equal(shared.Id, marys.Id);

    var site = await ExportAsync();

    var path = $"media/{shared.Id}/marriage record.pdf";
    Assert.Single(site.Names, name => name == path);
    Assert.Equal(site.Names.Length, site.Names.Distinct().Count());
    var href = $"media/{shared.Id}/marriage%20record.pdf";
    Assert.Contains(href, site.PersonPage(john));
    Assert.Contains(href, site.PersonPage(mary));
  }

  [Fact]
  public async Task PersonPage_ReadsAsTheAppDoes()
  {
    var john = await PersonAsync("John");
    var services = new TestServices().Provider;
    var nameFormatter = services.GetRequiredService<INameFormatter>();
    var dateFormatter = services.GetRequiredService<IDateFormatter>();

    var site = await ExportAsync();

    var page = site.PersonPage(john);
    var fullName = nameFormatter.ToString(john, NameFormat.FullPersonName);
    var birthDate = dateFormatter.ToString(john.BirthDate);
    var encodedFullName = System.Net.WebUtility.HtmlEncode(fullName);
    var encodedBirthDate = System.Net.WebUtility.HtmlEncode(birthDate);
    Assert.Contains(encodedFullName, page);
    Assert.Contains(encodedBirthDate, page);
  }

  [Fact]
  public async Task PersonPage_ListsRelativesAsLinksToTheirPages()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var tom = await PersonAsync("Tom");

    var site = await ExportAsync();

    var page = site.PersonPage(john);
    Assert.Contains($"href=\"person-{mary.Id}.html\"", page);
    Assert.Contains($"href=\"person-{tom.Id}.html\"", page);
  }

  [Fact]
  public async Task PersonPage_HeadsWithThePortraitOrTheDefaultPhotoForTheSex()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var solo = await PersonAsync("Solo");
    var viewer = ViewerHref(john.MainPhoto!);
    var portrait = $"<a class=\"portrait\" href=\"{viewer}\" target=\"_blank\"><img src=\"media/{john.MainPhoto!.Id}/photo.png\"";

    var site = await ExportAsync();

    var johnsPage = site.PersonPage(john);
    var marysPage = site.PersonPage(mary);
    var solosPage = site.PersonPage(solo);
    Assert.Contains(portrait, johnsPage);
    Assert.Contains("<span class=\"portrait stub\" style=\"background-image:url('media/female_stub.png')\"></span>", marysPage);
    Assert.Contains("<span class=\"portrait\"></span>", solosPage);
  }

  [Fact]
  public async Task PortraitAndGallery_OpenTheOneViewerPageInANewWindow()
  {
    var john = await PersonAsync("John");
    var viewer = ViewerHref(john.MainPhoto!);

    var site = await ExportAsync();

    var page = site.PersonPage(john);
    Assert.Contains($"<a class=\"portrait\" href=\"{viewer}\" target=\"_blank\">", page);
    Assert.Contains($"<figure><a href=\"{viewer}\" target=\"_blank\">", page);
    var viewerPage = Assert.Single(site.Pages, name => name.StartsWith("photo"));
    Assert.Equal("photo.html", viewerPage);
  }

  [Fact]
  public async Task ViewerLink_CarriesThePhotosCaption()
  {
    var document = await ImportAsync(CaptionedGedcom);
    var john = await PersonAsync("John", document);
    var viewer = ViewerHref(john.MainPhoto!, "John at the mill");

    var site = await ExportAsync(document: document);

    var page = site.PersonPage(john);
    Assert.Contains($"<a class=\"portrait\" href=\"{viewer}\"", page);
    Assert.Contains($"<figure><a href=\"{viewer}\"", page);
  }

  [Fact]
  public async Task Viewer_IsTitledInTheAppsLanguage()
  {
    var site = await ExportAsync();

    var viewer = site.Page("photo.html");
    var title = System.Net.WebUtility.HtmlEncode(UIStrings.FieldPersonPhotos);
    Assert.Contains($"<title>{title}</title>", viewer);
  }

  [Fact]
  public async Task Avatars_KeepLinkingToThePersonPage()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");

    var site = await ExportAsync();

    var card = Card(site.PersonPage(mary), john);
    Assert.Contains("class=\"avatar\"", card);
    Assert.DoesNotContain("photo-", card);
  }

  [Fact]
  public async Task PersonPage_MarksBirthAndDeathOnlyWhereRecorded()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var birthLabel = System.Net.WebUtility.HtmlEncode(UIStrings.FieldDateOfBirth);
    var deathLabel = System.Net.WebUtility.HtmlEncode(UIStrings.FieldDateOfDeath);

    var site = await ExportAsync();

    var johnsPage = site.PersonPage(john);
    var marysPage = site.PersonPage(mary);
    Assert.Contains($"<div class=\"birth\"><dt>{birthLabel}</dt>", johnsPage);
    Assert.Contains($"<div class=\"death\"><dt>{deathLabel}</dt>", johnsPage);
    Assert.DoesNotContain("class=\"death\"", marysPage);
  }

  [Fact]
  public async Task PersonPage_HeadsPhotosOnlyWhenThereAreAny()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");

    var site = await ExportAsync();

    var heading = Heading(UIStrings.FieldPersonPhotos);
    Assert.Contains(heading, site.PersonPage(john));
    Assert.DoesNotContain(heading, site.PersonPage(mary));
  }

  // The portrait already heads the page, so the relatives come first.
  [Fact]
  public async Task PersonPage_ListsRelativesBeforePhotos()
  {
    var john = await PersonAsync("John");

    var site = await ExportAsync();

    var page = site.PersonPage(john);
    var relativesHeading = Heading(UIStrings.LblRelatives);
    var photosHeading = Heading(UIStrings.FieldPersonPhotos);
    var relatives = page.IndexOf(relativesHeading);
    var photos = page.IndexOf(photosHeading);
    Assert.InRange(relatives, 0, photos - 1);
  }

  [Fact]
  public async Task FamilyPage_HeadsItsMembers()
  {
    var families = await _Document.FamilyManager.GetFamiliesAsync(Token);
    var smiths = families.Single();

    var site = await ExportAsync();

    var page = site.Page($"family-{smiths.Id}.html");
    var heading = Heading(UIStrings.LblPersons);
    Assert.Contains(heading, page);
  }

  [Fact]
  public async Task BiographyLinks_BecomePageAndMediaLinks()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var photo = john.MainPhoto!;
    await SetBiographyAsync(john, $"[Mary](person:{mary.Id}) ![Portrait 50%](media:{photo.Id})");

    var site = await ExportAsync();

    var page = site.PersonPage(john);
    Assert.Contains($"href=\"person-{mary.Id}.html\"", page);
    Assert.Contains($"src=\"media/{photo.Id}/photo.png\"", page);
    Assert.Contains("width:320px;max-width:50%", page);
    Assert.Contains("alt=\"Portrait\"", page);
  }

  [Fact]
  public async Task Biography_KeepsNoScriptTagAttributeOrUnsafeLink()
  {
    var john = await PersonAsync("John");
    await SetBiographyAsync(john, """
      Hello <script>alert(1)</script> world.

      [bad](javascript:alert(2)) <javascript:alert(3)>

      # Heading {onclick=alert(4)}
      """);

    var site = await ExportAsync();

    var page = site.PersonPage(john);
    Assert.DoesNotContain("<script", page);
    Assert.DoesNotContain("href=\"javascript:", page);
    Assert.DoesNotContain("onclick", page);
    Assert.Contains("bad", page);
  }

  // As in the app, where a link to a person since deleted is simply inert.
  [Fact]
  public async Task DanglingPersonLink_BecomesText()
  {
    var john = await PersonAsync("John");
    await SetBiographyAsync(john, "[Somebody](person:99999)");

    var site = await ExportAsync();

    var page = site.PersonPage(john);
    Assert.Contains("Somebody", page);
    Assert.DoesNotContain("person-99999.html", page);
  }

  [Fact]
  public async Task DanglingImage_RendersNothing()
  {
    var john = await PersonAsync("John");
    await SetBiographyAsync(john, "Before ![Lost](media:99999) after");

    var site = await ExportAsync();

    // John's own portrait is an <img> too, but it sits above the biography.
    var page = site.PersonPage(john);
    var biography = page.Split("Before")[1];
    Assert.DoesNotContain("<img", biography);
    Assert.DoesNotContain("Lost", page);
  }

  [Fact]
  public async Task Index_ListsEveryFamilyIncludingTheNoFamilyBucket()
  {
    var solo = await PersonAsync("Solo");

    var site = await ExportAsync();

    var index = site.Page("index.html");
    var noFamily = site.Page("family-0.html");
    Assert.Contains("href=\"family-0.html\"", index);
    Assert.Contains($"href=\"person-{solo.Id}.html\"", noFamily);
  }

  [Fact]
  public async Task Index_NamesPersonsByTheirCommonName()
  {
    var john = await PersonAsync("John");
    var nameFormatter = new TestServices().Provider.GetRequiredService<INameFormatter>();

    var site = await ExportAsync();

    var commonName = nameFormatter.ToString(john, NameFormat.CommonPersonName);
    var encodedName = System.Net.WebUtility.HtmlEncode(commonName);
    var index = site.Page("index.html");
    var card = Card(index, john);
    Assert.Contains($"<span class=\"name\">{encodedName}</span>", card);
  }

  [Fact]
  public async Task Index_GroupsEveryPersonOnceUnderTheInitialOfTheirCommonName()
  {
    var persons = await _Document.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, Token);
    var nameFormatter = new TestServices().Provider.GetRequiredService<INameFormatter>();

    var site = await ExportAsync();

    var index = site.Page("index.html");
    var matches = InitialGroupPattern().Matches(index);
    var groups = matches.ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
    foreach (var person in persons)
    {
      var commonName = nameFormatter.ToString(person, NameFormat.CommonPersonName);
      var initial = commonName[..1].ToUpper();
      var group = groups[initial];
      Assert.Contains($"<a class=\"card\" href=\"person-{person.Id}.html\"", group);
    }
    var cardCount = groups.Values.Sum(group => CardPattern().Count(group));
    Assert.Equal(persons.Length, cardCount);
  }

  [Fact]
  public async Task PersonCards_ShowTheMainPhotoOrTheDefaultPhotoForTheSex()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var tom = await PersonAsync("Tom");
    var solo = await PersonAsync("Solo");
    var photo = $"src=\"media/{john.MainPhoto!.Id}/photo.png\"";

    var site = await ExportAsync();

    var index = site.Page("index.html");
    var johnsCard = Card(index, john);
    var marysCard = Card(index, mary);
    var tomsCard = Card(index, tom);
    var solosCard = Card(index, solo);
    Assert.Contains(photo, johnsCard);
    Assert.Contains("<span class=\"avatar stub\" style=\"background-image:url('media/female_stub.png')\"></span>", marysCard);
    Assert.Contains("<span class=\"avatar stub\" style=\"background-image:url('media/male_stub.png')\"></span>", tomsCard);
    Assert.Contains("<span class=\"avatar\"></span>", solosCard);
  }

  // Tom's photo-less male card appears on several pages, but the zip holds his stub once.
  [Fact]
  public async Task TheDefaultPhotos_AreWrittenOncePerSex()
  {
    var site = await ExportAsync();

    var stubs = site.Names.Where(name => name.EndsWith("_stub.png"));
    Assert.Equal(["media/female_stub.png", "media/male_stub.png"], stubs.Order());
    var male = site.Content("media/male_stub.png");
    Assert.NotNull(ImageUtils.PixelSize(male));
  }

  [Fact]
  public async Task WithoutAPhotolessPersonOfKnownSex_NoDefaultPhotoIsWritten()
  {
    var mary = await PersonAsync("Mary");
    var tom = await PersonAsync("Tom");
    await _Document.PersonManager.UpdatePersonAsync(mary with { BiologicalSex = BiologicalSex.Unknown }, Token);
    await _Document.PersonManager.UpdatePersonAsync(tom with { BiologicalSex = BiologicalSex.Unknown }, Token);

    var site = await ExportAsync();

    Assert.DoesNotContain(site.Names, name => name.EndsWith("_stub.png"));
  }

  [Fact]
  public async Task RelativeCards_ShowTheRelativesMainPhoto()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var photo = $"src=\"media/{john.MainPhoto!.Id}/photo.png\"";

    var site = await ExportAsync();

    var page = site.PersonPage(mary);
    var johnsCard = Card(page, john);
    Assert.Contains(photo, johnsCard);
  }

  [Fact]
  public async Task EveryPage_LinksTheStatisticsPage()
  {
    var site = await ExportAsync();

    Assert.Contains("statistics.html", site.Names);
    Assert.All(site.HeaderedPages, name =>
    {
      var page = site.Page(name);
      Assert.Contains("href=\"statistics.html\"", page);
    });
  }

  [Fact]
  public async Task StatisticsPage_CountsTheProject()
  {
    var site = await ExportAsync();

    var page = site.Page("statistics.html");
    var label = System.Net.WebUtility.HtmlEncode(UIStrings.FieldStatTotalPersons);
    var families = System.Net.WebUtility.HtmlEncode(UIStrings.FieldStatTotalFamilies);
    Assert.Contains($"<dt>{label}</dt><dd>4</dd>", page);
    Assert.Contains($"<dt>{families}</dt><dd>1</dd>", page);
  }

  // John's is the only known birth, so his decade's bar is the busiest: 1 / 1.1 of its row.
  [Fact]
  public async Task DecadeBars_KeepADotDecimalWidthUnderACommaDecimalCulture()
  {
    var culture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    Site site;
    try
    {
      site = await ExportAsync();
    }
    finally
    {
      CultureInfo.CurrentCulture = culture;
    }

    var page = site.Page("statistics.html");
    Assert.Contains("style=\"width:90.9%\"", page);
  }

  [Fact]
  public async Task TextFilledIntoTemplates_IsEncoded()
  {
    var site = await ExportAsync(projectName: "<b>Smiths</b>");

    var index = site.Page("index.html");
    Assert.Contains("<title>&lt;b&gt;Smiths&lt;/b&gt;</title>", index);
    Assert.Contains("<h1>&lt;b&gt;Smiths&lt;/b&gt;</h1>", index);
    Assert.DoesNotContain("<b>", index);
  }

  [Fact]
  public async Task WithAMainPerson_EveryPageLinksTheMainPersonPage()
  {
    var john = await PersonAsync("John");

    var site = await ExportAsync(mainPerson: MainPerson(john));

    Assert.Contains("main-person.html", site.Names);
    Assert.All(site.HeaderedPages, name =>
    {
      var page = site.Page(name);
      Assert.Contains("href=\"main-person.html\"", page);
    });
  }

  [Fact]
  public async Task WithoutAMainPerson_NoPageLinksAMainPersonPage()
  {
    var site = await ExportAsync();

    Assert.DoesNotContain("main-person.html", site.Names);
    Assert.All(site.Pages, name =>
    {
      var page = site.Page(name);
      Assert.DoesNotContain("main-person.html", page);
    });
  }

  [Fact]
  public async Task OnlyTheMainPersonsOwnPage_LinksTheMainPersonPageBesideItsRelatives()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    const string link = "<p class=\"more\"><a href=\"main-person.html\">";

    var site = await ExportAsync(mainPerson: MainPerson(john));

    Assert.Contains(link, site.PersonPage(john));
    Assert.DoesNotContain(link, site.PersonPage(mary));
  }

  // Old is listed by expanding John, yet labelled as Tom's grandfather.
  [Fact]
  public async Task AnExpandedRow_IsLabelledFromTheMainPerson()
  {
    var document = await ImportAsync(LineageGedcom);
    var tom = await PersonAsync("Tom", document);
    var old = await PersonAsync("Old", document);
    var father = tom.RelativeInfos.Single(relative => relative.Type == RelationshipType.Parent && relative.BiologicalSex == BiologicalSex.Male);
    var fathersRelatives = await document.RelativesProvider.GetRelativeInfosAsync(father, false, Token);
    var grandfather = fathersRelatives.Single(relative => relative.Id == old.Id);
    var formatter = new TestServices().Provider.GetRequiredService<IRelationshipTypeFormatter>();
    var label = formatter.ToString(grandfather.Type, grandfather.BiologicalSex, grandfather.Generation, grandfather.Consanguinity);
    var encodedLabel = System.Net.WebUtility.HtmlEncode(label);

    var site = await ExportAsync(document: document, mainPerson: MainPerson(tom));

    var row = Row(site.Page("main-person.html"), old);
    Assert.Equal(new Generation(2), grandfather.Generation);
    Assert.StartsWith("<li style=\"--depth:1\">", row);
    Assert.Contains($"<span class=\"relation\">{encodedLabel}", row);
  }

  // The walk fetches relatives without their photos.
  [Fact]
  public async Task AnExpandedRow_ShowsTheRelativesMainPhoto()
  {
    var document = await ImportAsync(LineageGedcom);
    var tom = await PersonAsync("Tom", document);
    var old = await PersonAsync("Old", document);

    var site = await ExportAsync(document: document, mainPerson: MainPerson(tom));

    var row = Row(site.Page("main-person.html"), old);
    Assert.Contains($"src=\"media/{old.MainPhoto!.Id}/photo.png\"", row);
  }

  [Fact]
  public async Task WithoutAPhoto_AnExpandedRowAndATreeCard_ShowTheDefaultPhotoForTheSex()
  {
    var document = await ImportAsync(LineageGedcom);
    var tom = await PersonAsync("Tom", document);
    var mary = await PersonAsync("Mary", document);
    var stub = "<span class=\"avatar stub\" style=\"background-image:url('media/female_stub.png')\"></span>";

    var site = await ExportAsync(document: document, mainPerson: MainPerson(tom));

    var page = site.Page("main-person.html");
    var row = Row(page, mary);
    var start = page.IndexOf($"<a class=\"tree-node\" href=\"person-{mary.Id}.html\"");
    var end = page.IndexOf("</a>", start);
    var treeCard = page[start..end];
    Assert.Contains(stub, row);
    Assert.Contains(stub, treeCard);
  }

  [Fact]
  public async Task AListUnderTheCap_CarriesNoNote()
  {
    var document = await ImportAsync(LineageGedcom);
    var tom = await PersonAsync("Tom", document);

    var site = await ExportAsync(document: document, mainPerson: MainPerson(tom));

    var page = site.Page("main-person.html");
    Assert.DoesNotContain("class=\"note\"", page);
  }

  [Fact]
  public async Task AListCutByTheCap_EndsWithANote()
  {
    var document = await ImportAsync(DescendantsGedcom());
    var root = await PersonAsync("Root", document);
    var note = string.Format(UIStrings.HintRelativesTruncated_1, 500);
    var encodedNote = System.Net.WebUtility.HtmlEncode(note);

    var site = await ExportAsync(document: document, mainPerson: MainPerson(root));

    var page = site.Page("main-person.html");
    Assert.Equal(500, CardPattern().Count(page));
    Assert.Contains($"<p class=\"note\">{encodedNote}</p>", page);
  }

  // Seven generations hold 255 persons; the eighth would take the tree to 511.
  [Fact]
  public async Task TheTree_KeepsTheDeepestGenerationWithinTheNodeBudget()
  {
    var document = await ImportAsync(DescendantsGedcom());
    var root = await PersonAsync("Root", document);

    var site = await ExportAsync(document: document, mainPerson: MainPerson(root));

    var lefts = TreeNodeLefts(site.Page("main-person.html"));
    Assert.Equal(255, lefts.Count);
  }

  [Fact]
  public async Task TheTree_HasOneCardPerPersonLinkingToTheirPage()
  {
    var document = await ImportAsync(LineageGedcom);
    var persons = await document.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, Token);
    var tom = await PersonAsync("Tom", document);

    var site = await ExportAsync(document: document, mainPerson: MainPerson(tom));

    var page = site.Page("main-person.html");
    var lefts = TreeNodeLefts(page);
    var personIds = persons.Select(person => person.Id);
    Assert.Equal(personIds.Order(), lefts.Keys.Order());
    Assert.Contains($"<a class=\"tree-node main\" href=\"person-{tom.Id}.html\"", page);
  }

  [Fact]
  public async Task AHiddenPerson_HasNoTreeCardButStaysInTheRelativesList()
  {
    var document = await ImportAsync(LineageGedcom);
    var tom = await PersonAsync("Tom", document);
    var old = await PersonAsync("Old", document);

    var site = await ExportAsync(document: document, mainPerson: MainPerson(tom, hiddenIds: [old.Id]));

    var page = site.Page("main-person.html");
    var lefts = TreeNodeLefts(page);
    Assert.DoesNotContain(old.Id, lefts.Keys);
    Assert.Contains(tom.Id, lefts.Keys);
    Assert.Contains($"<a class=\"card\" href=\"person-{old.Id}.html\"", page);
  }

  [Fact]
  public async Task ASavedArrangement_PlacesThePinnedCardInItsColumn()
  {
    var document = await ImportAsync(LineageGedcom);
    var tom = await PersonAsync("Tom", document);
    var mary = await PersonAsync("Mary", document);
    var pitch = new FamilyTreeLayoutMetrics().SlotPitch;

    var site = await ExportAsync(document: document, mainPerson: MainPerson(tom, pins: new() { [mary.Id] = 3 }));

    var lefts = TreeNodeLefts(site.Page("main-person.html"));
    Assert.Equal(lefts[tom.Id] + (3 * pitch), lefts[mary.Id]);
  }

  [Fact]
  public async Task CancelledExport_Throws()
  {
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExportAsync(cancellation.Token));
  }
}
