using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Gedcom.Extensions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.UI.HtmlExport;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.DependencyInjection;
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

  private readonly string _Folder = Path.Combine(Path.GetTempPath(), $"gt4_html_{Guid.NewGuid():N}");
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

    var factory = new TestServices().Provider.GetRequiredService<IProjectDocumentFactory>();
    var projectPath = Path.Combine(_Folder, "project.gt4");
    _Document = await factory.CreateNewAsync(projectPath, "Smiths", Token);
    var importer = new ServiceCollection().AddGedcom().BuildServiceProvider().GetRequiredService<IGedcomImporter>();
    using var reader = new StringReader(Gedcom);
    await importer.ImportAsync(_Document, reader, Token, _Folder);
  }

  public async ValueTask DisposeAsync()
  {
    await _Document.DisposeAsync();
    try { Directory.Delete(_Folder, recursive: true); } catch { /* best-effort temp cleanup */ }
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

  private async Task<PersonFullInfo> PersonAsync(string givenName)
  {
    var persons = await _Document.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, Token);
    var person = persons.Single(p => p.Names.Any(name => name.Value == givenName));
    return await _Document.PersonManager.GetPersonFullInfoAsync(person, Token);
  }

  private async Task SetBiographyAsync(PersonFullInfo person, string markdown)
  {
    var content = Encoding.UTF8.GetBytes(markdown);
    var biography = new Data(ElementId.NonCommittedId, content, "text/plain", DataCategory.PersonBio);
    await _Document.PersonManager.UpdatePersonAsync(person with { Biography = biography }, Token);
  }

  private async Task<Site> ExportAsync(CancellationToken token = default, string projectName = "Smiths")
  {
    var exporter = new TestServices().Provider.GetRequiredService<HtmlExporter>();
    using var output = new MemoryStream();
    await exporter.ExportAsync(_Document, projectName, output, token);

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

    public string Page(string name)
    {
      var page = entries.Single(entry => entry.Name == name);
      return Encoding.UTF8.GetString(page.Content);
    }

    public string PersonPage(Person person) => Page($"person-{person.Id}.html");

    public IEnumerable<string> Pages => entries.Where(entry => entry.Name.EndsWith(".html")).Select(entry => entry.Name);
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

  [GeneratedRegex("(?:href|src)=\"([^\"]*)\"")]
  private static partial Regex LinkPattern();

  [GeneratedRegex("<section class=\"initial\" id=\"[^\"]*\">\n<h3>([^<]*)</h3>(.*?)</section>", RegexOptions.Singleline)]
  private static partial Regex InitialGroupPattern();

  [GeneratedRegex("<a class=\"card\"")]
  private static partial Regex CardPattern();

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
      var targets = pageLinks.Except(fragments).Select(Uri.UnescapeDataString);
      Assert.All(fragments, fragment => Assert.Contains($"id=\"{fragment[1..]}\"", page));
      Assert.All(targets, target => Assert.Contains(target, site.Names));
      links.AddRange(pageLinks);
    }
    Assert.Contains(links, link => link.StartsWith("media/"));
    Assert.Contains(links, link => link.StartsWith('#'));
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
  public async Task PersonPage_HeadsWithThePortraitOrASilhouette()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var portrait = $"<a class=\"portrait\" href=\"media/{john.MainPhoto!.Id}/photo.png\">";

    var site = await ExportAsync();

    var johnsPage = site.PersonPage(john);
    var marysPage = site.PersonPage(mary);
    Assert.Contains(portrait, johnsPage);
    Assert.Contains("<span class=\"portrait\"></span>", marysPage);
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
  public async Task PersonCards_ShowTheMainPhotoOrAnEmptyAvatar()
  {
    var john = await PersonAsync("John");
    var mary = await PersonAsync("Mary");
    var photo = $"src=\"media/{john.MainPhoto!.Id}/photo.png\"";

    var site = await ExportAsync();

    var index = site.Page("index.html");
    var johnsCard = Card(index, john);
    var marysCard = Card(index, mary);
    Assert.Contains(photo, johnsCard);
    Assert.Contains("<span class=\"avatar\"></span>", marysCard);
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
  public async Task TextFilledIntoTemplates_IsEncoded()
  {
    var site = await ExportAsync(projectName: "<b>Smiths</b>");

    var index = site.Page("index.html");
    Assert.Contains("<title>&lt;b&gt;Smiths&lt;/b&gt;</title>", index);
    Assert.Contains("<h1>&lt;b&gt;Smiths&lt;/b&gt;</h1>", index);
    Assert.DoesNotContain("<b>", index);
  }

  [Fact]
  public async Task CancelledExport_Throws()
  {
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExportAsync(cancellation.Token));
  }
}
