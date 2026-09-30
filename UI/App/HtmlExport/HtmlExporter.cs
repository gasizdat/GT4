using GT4.Core.Gedcom;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace GT4.UI.HtmlExport;

/// <summary>
/// Renders the whole project as a zipped static site. Pages go through the app's own formatters and
/// display rules, so they read the way the screens they mirror do.
/// </summary>
public sealed class HtmlExporter
{
  private const string IndexPage = "index.html";
  private const string StatisticsPage = "statistics.html";
  private const string StyleSheet = "style.css";
  private const string MediaFolder = "media";

  private static readonly HtmlTemplate DocumentTemplate = HtmlTemplate.Load("document.html");
  private static readonly HtmlTemplate IndexPageTemplate = HtmlTemplate.Load("index-page.html");
  private static readonly HtmlTemplate FamilyPageTemplate = HtmlTemplate.Load("family-page.html");
  private static readonly HtmlTemplate PersonPageTemplate = HtmlTemplate.Load("person-page.html");
  private static readonly HtmlTemplate StatisticsPageTemplate = HtmlTemplate.Load("statistics-page.html");
  private static readonly HtmlTemplate DefinitionsTemplate = HtmlTemplate.Load("definitions.html");
  private static readonly HtmlTemplate DecadeItemTemplate = HtmlTemplate.Load("decade-item.html");
  private static readonly HtmlTemplate NavigationTemplate = HtmlTemplate.Load("navigation.html");
  private static readonly HtmlTemplate NavigationFamilyTemplate = HtmlTemplate.Load("navigation-family.html");
  private static readonly HtmlTemplate SectionTemplate = HtmlTemplate.Load("section.html");
  private static readonly HtmlTemplate ListTemplate = HtmlTemplate.Load("list.html");
  private static readonly HtmlTemplate FieldTemplate = HtmlTemplate.Load("field.html");
  private static readonly HtmlTemplate FamilyItemTemplate = HtmlTemplate.Load("family-item.html");
  private static readonly HtmlTemplate InitialsTemplate = HtmlTemplate.Load("initials.html");
  private static readonly HtmlTemplate InitialLinkTemplate = HtmlTemplate.Load("initial-link.html");
  private static readonly HtmlTemplate InitialGroupTemplate = HtmlTemplate.Load("initial-group.html");
  private static readonly HtmlTemplate PersonItemTemplate = HtmlTemplate.Load("person-item.html");
  private static readonly HtmlTemplate RelativeItemTemplate = HtmlTemplate.Load("relative-item.html");
  private static readonly HtmlTemplate AvatarTemplate = HtmlTemplate.Load("avatar.html");
  private static readonly HtmlTemplate AvatarEmptyTemplate = HtmlTemplate.Load("avatar-empty.html");
  private static readonly HtmlTemplate AttachmentItemTemplate = HtmlTemplate.Load("attachment-item.html");
  private static readonly HtmlTemplate AttachmentFileNameTemplate = HtmlTemplate.Load("attachment-file-name.html");
  private static readonly HtmlTemplate PortraitTemplate = HtmlTemplate.Load("portrait.html");
  private static readonly HtmlTemplate PortraitEmptyTemplate = HtmlTemplate.Load("portrait-empty.html");
  private static readonly HtmlTemplate GalleryTemplate = HtmlTemplate.Load("gallery.html");
  private static readonly HtmlTemplate FigureTemplate = HtmlTemplate.Load("figure.html");
  private static readonly HtmlTemplate FigcaptionTemplate = HtmlTemplate.Load("figcaption.html");
  private static readonly HtmlTemplate ProseTemplate = HtmlTemplate.Load("prose.html");

  // A biography link to any other scheme renders as text.
  private static readonly string[] SafeSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto, "tel"];

  private readonly INameFormatter _NameFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly ILifeDatesFormatter _LifeDatesFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly DataConverterResolver _DataConverterResolver;
  private readonly IComparer<Name> _NameComparer;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IComparer<PersonInfo> _PersonInfoComparerByShortNames;

  public HtmlExporter(
    INameFormatter nameFormatter,
    IDateFormatter dateFormatter,
    IDateSpanFormatter dateSpanFormatter,
    ILifeDatesFormatter lifeDatesFormatter,
    IRelationshipTypeFormatter relationshipTypeFormatter,
    DataConverterResolver dataConverterResolver,
    IComparer<Name> nameComparer,
    IComparer<PersonInfo> personInfoComparer,
    [FromKeyedServices(NameFormat.ShortPersonName)]
    IComparer<PersonInfo> personInfoComparerByShortNames)
  {
    _NameFormatter = nameFormatter;
    _DateFormatter = dateFormatter;
    _DateSpanFormatter = dateSpanFormatter;
    _LifeDatesFormatter = lifeDatesFormatter;
    _RelationshipTypeFormatter = relationshipTypeFormatter;
    _DataConverterResolver = dataConverterResolver;
    _NameComparer = nameComparer;
    _PersonInfoComparer = personInfoComparer;
    _PersonInfoComparerByShortNames = personInfoComparerByShortNames;
  }

  public async Task ExportAsync(IProjectDocument document, string projectName, Stream output, CancellationToken token)
  {
    using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
    var persons = await document.PersonManager.GetPersonInfosAsync(selectMainPhoto: true, token);
    var families = await document.FamilyManager.GetFamiliesAsync(token);
    HashSet<int> personIds = [.. persons.Select(person => person.Id)];
    var site = new Site(archive, document, projectName, personIds, token);

    // Grouped as ProjectPage's family cards are, "No family" bucket included.
    var membersByNameId = persons
      .SelectMany(person => person.Names.Select(name => (NameId: name.Id, Person: person)))
      .ToLookup(x => x.NameId, x => x.Person);
    var familyMembers = families
      .OrderBy(family => family, _NameComparer)
      .Select(family => (Family: (Name)family, Members: membersByNameId[family.Id].ToArray()))
      .ToList();
    PersonInfo[] familyless = [.. persons.Where(NoFamily.Includes)];
    if (familyless.Length > 0)
    {
      familyMembers.Add((NoFamily.Name, familyless));
    }

    Name[] indexedFamilies = [.. familyMembers.Select(f => f.Family)];
    var index = await RenderIndexAsync(site, indexedFamilies, persons);
    var css = HtmlTemplate.ReadResource(StyleSheet);
    await site.WriteTextAsync(StyleSheet, css);
    await site.WriteTextAsync(IndexPage, index);
    var statistics = await RenderStatisticsAsync(site, persons, families);
    await site.WriteTextAsync(StatisticsPage, statistics);
    foreach (var (family, members) in familyMembers)
    {
      var page = await RenderFamilyAsync(site, family, members);
      var path = FamilyHref(family.Id);
      await site.WriteTextAsync(path, page);
    }
    foreach (var person in persons)
    {
      var page = await RenderPersonAsync(site, person);
      var path = PersonHref(person.Id);
      await site.WriteTextAsync(path, page);
    }
  }

  private static string FamilyHref(int familyId) => $"family-{familyId}.html";

  private static string PersonHref(int personId) => $"person-{personId}.html";

  private static string RenderDocument(string title, HtmlContent navigation, HtmlContent body)
  {
    var document = DocumentTemplate.Fill(("title", title), ("navigation", navigation), ("body", body));
    return document.Markup;
  }

  private static HtmlContent RenderNavigation(Site site, IEnumerable<Name> families)
  {
    var crumbs = families.Select(family => RenderFamilyLink(NavigationFamilyTemplate, family));
    var joined = HtmlContent.Join(crumbs);
    return NavigationTemplate.Fill(("project", site.ProjectName), ("families", joined), ("statistics", UIStrings.TitleStatisticsPage));
  }

  private static HtmlContent RenderFamilyLink(HtmlTemplate template, Name family)
  {
    var href = FamilyHref(family.Id);
    return template.Fill(("href", href), ("family", family.Value));
  }

  private static HtmlContent RenderSection(string heading, HtmlContent content) =>
    SectionTemplate.Fill(("heading", heading), ("content", content));

  private static HtmlContent RenderList(string listClass, IEnumerable<HtmlContent> items)
  {
    var joined = HtmlContent.Join(items);
    return ListTemplate.Fill(("class", listClass), ("items", joined));
  }

  private static async Task<HtmlContent> RenderAvatarAsync(Site site, Data? mainPhoto)
  {
    if (mainPhoto is null)
      return AvatarEmptyTemplate.Fill();

    var media = await site.WriteMediaAsync(mainPhoto);
    return AvatarTemplate.Fill(("src", media.Href));
  }

  private async Task<string> RenderIndexAsync(Site site, Name[] families, PersonInfo[] persons)
  {
    var familyItems = families.Select(family => RenderFamilyLink(FamilyItemTemplate, family));
    var familyList = RenderList("chips", familyItems);
    var familySection = RenderSection(UIStrings.TitleFamiliesPage, familyList);
    // Common names, not the family cards' short ones: outside a family, two Annes are no longer told apart.
    var groups = persons
      .OrderBy(person => person, _PersonInfoComparer)
      .GroupBy(InitialOf);
    var links = new List<HtmlContent>();
    var sections = new List<HtmlContent>();
    foreach (var (index, group) in groups.Index())
    {
      var id = $"initial-{index}";
      var link = InitialLinkTemplate.Fill(("id", id), ("initial", group.Key));
      var personList = await RenderPersonListAsync(site, group, NameFormat.CommonPersonName, _PersonInfoComparer, "rows");
      var section = InitialGroupTemplate.Fill(("id", id), ("initial", group.Key), ("persons", personList));
      links.Add(link);
      sections.Add(section);
    }
    var joinedLinks = HtmlContent.Join(links);
    var initials = InitialsTemplate.Fill(("links", joinedLinks));
    var personContent = HtmlContent.Join([initials, .. sections]);
    var personSection = RenderSection(UIStrings.LblPersons, personContent);
    var navigation = RenderNavigation(site, []);
    var body = IndexPageTemplate.Fill(("project", site.ProjectName), ("families", familySection), ("persons", personSection));
    return RenderDocument(site.ProjectName, navigation, body);
  }

  private string InitialOf(PersonInfo person)
  {
    var name = _NameFormatter.ToString(person, NameFormat.CommonPersonName);
    if (name.Length == 0)
      return "?";

    var initial = StringInfo.GetNextTextElement(name);
    return initial.ToUpper(CultureInfo.CurrentCulture);
  }

  private async Task<HtmlContent> RenderPersonListAsync(
    Site site,
    IEnumerable<PersonInfo> persons,
    NameFormat nameFormat,
    IComparer<PersonInfo> comparer,
    string listClass)
  {
    var items = new List<HtmlContent>();
    foreach (var person in persons.OrderBy(person => person, comparer))
    {
      var item = await RenderPersonItemAsync(site, person, nameFormat);
      items.Add(item);
    }
    return RenderList(listClass, items);
  }

  private async Task<HtmlContent> RenderPersonItemAsync(Site site, PersonInfo person, NameFormat nameFormat)
  {
    var href = PersonHref(person.Id);
    var avatar = await RenderAvatarAsync(site, person.MainPhoto);
    var name = _NameFormatter.ToString(person, nameFormat);
    var dates = _LifeDatesFormatter.ToString(person, showDeathDate: true, showAge: true);
    return PersonItemTemplate.Fill(("href", href), ("avatar", avatar), ("name", name), ("dates", dates));
  }

  // The sections and labels of StatisticsPage, in its order.
  private async Task<string> RenderStatisticsAsync(Site site, PersonInfo[] persons, Name[] families)
  {
    var relatives = await site.Document.Relatives.GetRelativesForPersonsAsync(persons, site.Token);
    var statistics = ProjectStatisticsCalculator.Compute(persons, families, relatives);
    var text = new ProjectStatisticsText(statistics, _NameFormatter);
    var decades = RenderDecades(text);
    HtmlContent[] sections =
    [
      RenderStatisticsSection(UIStrings.FieldStatOverview, "tiles",
        (UIStrings.FieldStatTotalPersons, text.TotalPersons),
        (UIStrings.FieldStatTotalFamilies, text.TotalFamilies),
        (UIStrings.FieldStatMenCount, text.MenCount),
        (UIStrings.FieldStatWomenCount, text.WomenCount),
        (UIStrings.FieldStatUnknownSexCount, text.UnknownSexCount),
        (UIStrings.FieldStatLivingCount, text.LivingCount)),
      RenderStatisticsSection(UIStrings.FieldStatAge, "stats",
        (UIStrings.FieldStatAverageLifespan, text.AverageLifespan),
        (UIStrings.FieldStatLifespan95thPercentile, text.Lifespan95thPercentile),
        (UIStrings.FieldStatOldestLiving, text.OldestLiving),
        (UIStrings.FieldStatLongestLifespan, text.LongestLifespan)),
      RenderStatisticsSection(UIStrings.FieldStatBirthYears, "stats",
        (UIStrings.FieldStatBirthYearSpan, text.BirthYearSpan),
        (UIStrings.FieldStatMedianBirthYear, text.MedianBirthYear),
        (UIStrings.FieldStatBirthsByDecade, decades)),
      RenderStatisticsSection(UIStrings.FieldStatNames, "stats",
        (UIStrings.FieldStatLargestFamily, text.TopLargestFamilies),
        (UIStrings.FieldStatSingleMemberFamilies, text.SingleMemberFamilies),
        (UIStrings.FieldStatTopMaleFirstNames, text.TopMaleFirstNames),
        (UIStrings.FieldStatTopFemaleFirstNames, text.TopFemaleFirstNames)),
      RenderStatisticsSection(UIStrings.FieldStatDataCompleteness, "stats",
        (UIStrings.FieldStatIncompleteBirthDates, text.IncompleteBirthDateCount),
        (UIStrings.FieldStatPhotoCoverage, text.PhotoCoverage),
        (UIStrings.FieldStatIsolatedPersons, text.IsolatedPersonCount)),
      RenderStatisticsSection(UIStrings.FieldStatRelationships, "stats",
        (UIStrings.FieldStatMarriageCount, text.MarriageCount),
        (UIStrings.FieldStatAverageChildren, text.AverageChildren),
        (UIStrings.FieldStatMostChildren, text.MostChildren)),
    ];

    var joined = HtmlContent.Join(sections);
    var navigation = RenderNavigation(site, []);
    var body = StatisticsPageTemplate.Fill(("title", UIStrings.TitleStatisticsPage), ("sections", joined));
    return RenderDocument(UIStrings.TitleStatisticsPage, navigation, body);
  }

  private static HtmlContent RenderStatisticsSection(string heading, string listClass, params (string Label, HtmlContent Value)[] fields)
  {
    var items = fields.Select(field => FieldTemplate.Fill(("class", string.Empty), ("label", field.Label), ("value", field.Value)));
    var joined = HtmlContent.Join(items);
    var list = DefinitionsTemplate.Fill(("class", listClass), ("fields", joined));
    return RenderSection(heading, list);
  }

  private static HtmlContent RenderDecades(ProjectStatisticsText text)
  {
    var decades = text.BirthsByDecade;
    if (decades.Length == 0)
      return UIStrings.StatValueNone;

    var rowLength = text.DecadeRowLength;
    var items = decades.Select(births => RenderDecade(births, rowLength));
    return RenderList("decades", items);
  }

  // A CSS length, so the invariant culture: under a comma-decimal one the browser would drop the width.
  private static HtmlContent RenderDecade((string Decade, int Count) births, double rowLength)
  {
    var percent = births.Count / rowLength * 100;
    var width = percent.ToString("0.#", CultureInfo.InvariantCulture);
    var count = births.Count.ToString();
    return DecadeItemTemplate.Fill(("decade", births.Decade), ("width", width), ("count", count));
  }

  private async Task<string> RenderFamilyAsync(Site site, Name family, PersonInfo[] members)
  {
    // The "No family" bucket has no row of its own, so no media either.
    var info = family.Id == NoFamily.Name.Id
      ? new FamilyFullInfo(family, null, [], [])
      : await site.Document.FamilyManager.GetFamilyFullInfoAsync(family, site.Token);
    var attachments = await ReadAttachmentsAsync(info.Attachments, site.Token);

    var navigation = RenderNavigation(site, []);
    var photos = await RenderPhotosAsync(site, info.MainPhoto, info.AdditionalPhotos);
    var attachmentList = await RenderAttachmentsAsync(site, attachments);
    var personList = await RenderPersonListAsync(site, members, NameFormat.ShortPersonName, _PersonInfoComparerByShortNames, "cards");
    var personSection = RenderSection(UIStrings.LblPersons, personList);
    var body = FamilyPageTemplate.Fill(
      ("family", family.Value),
      ("photos", photos),
      ("attachments", attachmentList),
      ("persons", personSection));
    return RenderDocument(family.Value, navigation, body);
  }

  // Gathers what PersonPage.GetPersonDataAsync does, so the sections hold what the page's do. Unlike the
  // page, the relatives come before the photos: the portrait already heads the page.
  private async Task<string> RenderPersonAsync(Site site, PersonInfo person)
  {
    var project = site.Document;
    var token = site.Token;
    var full = await project.PersonManager.GetPersonFullInfoAsync(person, token);
    var roots = await project.RelativesProvider.GetRootsAsync(full, token);
    var attachments = await ReadAttachmentsAsync(full.Attachments, token);
    var bioConverter = _DataConverterResolver(DataCategory.PersonBio);
    var gedcomConverter = _DataConverterResolver(DataCategory.PersonGedcomTags);
    var bio = await bioConverter.ToObjectAsync(full.Biography, token);
    var gedcomDetails = await gedcomConverter.ToObjectAsync(full.GedcomData, token);
    var familyDetails = await PersonFamilyDetails.ReadAsync(project, full, attachments, _NameFormatter, token);
    var biography = BiographySections.Combine(bio as string, gedcomDetails as string, familyDetails);

    var shortName = _NameFormatter.ToString(full, NameFormat.ShortPersonName);
    var fullName = _NameFormatter.ToString(full, NameFormat.FullPersonName);
    var families = full.Names.Where(name => name.Type.HasFlag(NameType.FamilyName)).DefaultIfEmpty(NoFamily.Name);
    var navigation = RenderNavigation(site, families);
    var portrait = await RenderPortraitAsync(site, full.MainPhoto);
    var dates = RenderDates(full);
    var photos = await RenderPhotosAsync(site, full.MainPhoto, full.AdditionalPhotos);
    var relatives = await RenderRelativesAsync(site, roots, full.BirthDate);
    var biographySection = await RenderBiographyAsync(site, biography);
    var attachmentList = await RenderAttachmentsAsync(site, attachments);
    var body = PersonPageTemplate.Fill(
      ("portrait", portrait),
      ("name", shortName),
      ("fullName", fullName),
      ("dates", dates),
      ("photos", photos),
      ("relatives", relatives),
      ("biography", biographySection),
      ("attachments", attachmentList));
    return RenderDocument(shortName, navigation, body);
  }

  // Not through the attachment converter: it would park every image attachment in the app's image cache.
  private static async Task<AttachmentInfo[]> ReadAttachmentsAsync(Data[] attachments, CancellationToken token)
  {
    var infos = new List<AttachmentInfo>();
    foreach (var data in attachments)
    {
      var fileName = await GedcomPhotoResidue.ExtractFileNameAsync(data, token);
      var title = await GedcomPhotoResidue.ExtractTitleAsync(data, token);
      infos.Add(new AttachmentInfo(fileName ?? string.Empty, title, data));
    }
    return [.. infos];
  }

  private HtmlContent RenderDates(PersonFullInfo person)
  {
    var birthDate = _DateFormatter.ToString(person.BirthDate);
    var span = person.DeathDate.GetValueOrDefault(Date.Now) - person.BirthDate;
    var age = _DateSpanFormatter.ToString(span);
    var birth = FieldTemplate.Fill(("class", "birth"), ("label", UIStrings.FieldDateOfBirth), ("value", birthDate));
    var fields = new List<HtmlContent> { birth };
    if (person.DeathDate.HasValue)
    {
      var deathDate = _DateFormatter.ToString(person.DeathDate);
      var death = FieldTemplate.Fill(("class", "death"), ("label", UIStrings.FieldDateOfDeath), ("value", deathDate));
      fields.Add(death);
    }
    var ageField = FieldTemplate.Fill(("class", string.Empty), ("label", UIStrings.FieldAge), ("value", age));
    fields.Add(ageField);
    return HtmlContent.Join(fields);
  }

  private static async Task<string?> ReadCaptionAsync(Site site, Data photo) =>
    photo.Category.IsTaggedPhoto() ? await GedcomPhotoResidue.ExtractTitleAsync(photo, site.Token) : null;

  private static async Task<HtmlContent> RenderPortraitAsync(Site site, Data? mainPhoto)
  {
    if (mainPhoto is null)
      return PortraitEmptyTemplate.Fill();

    var media = await site.WriteMediaAsync(mainPhoto);
    var caption = await ReadCaptionAsync(site, mainPhoto);
    return PortraitTemplate.Fill(("src", media.Href), ("caption", caption));
  }

  private static async Task<HtmlContent> RenderPhotosAsync(Site site, Data? mainPhoto, Data[] additionalPhotos)
  {
    Data[] photos = mainPhoto is null ? additionalPhotos : [mainPhoto, .. additionalPhotos];
    if (photos.Length == 0)
      return HtmlContent.Empty;

    var figures = new List<HtmlContent>();
    foreach (var photo in photos)
    {
      var media = await site.WriteMediaAsync(photo);
      var caption = await ReadCaptionAsync(site, photo);
      var figcaption = string.IsNullOrWhiteSpace(caption) ? HtmlContent.Empty : FigcaptionTemplate.Fill(("caption", caption));
      var figure = FigureTemplate.Fill(("src", media.Href), ("caption", caption), ("figcaption", figcaption));
      figures.Add(figure);
    }
    var joined = HtmlContent.Join(figures);
    var gallery = GalleryTemplate.Fill(("figures", joined));
    return RenderSection(UIStrings.FieldPersonPhotos, gallery);
  }

  // The first level of PersonPage's relatives tree, as it shows before anything is expanded.
  private async Task<HtmlContent> RenderRelativesAsync(Site site, RelativeInfo[] relatives, Date personBirthDate)
  {
    if (relatives.Length == 0)
      return HtmlContent.Empty;

    var items = new List<HtmlContent>();
    foreach (var relative in relatives)
    {
      var item = await RenderRelativeItemAsync(site, relative, personBirthDate);
      items.Add(item);
    }
    var list = RenderList("cards", items);
    return RenderSection(UIStrings.LblRelatives, list);
  }

  private async Task<HtmlContent> RenderRelativeItemAsync(Site site, RelativeInfo relative, Date personBirthDate)
  {
    var relation = new List<string>
    {
      _RelationshipTypeFormatter.ToString(relative.Type, relative.BiologicalSex, relative.Generation, relative.Consanguinity),
    };
    var bloodShare = relative.GetBloodShareText();
    if (bloodShare.Length > 0)
    {
      relation.Add(bloodShare);
    }
    if (relative.ShowsRelationshipDate(personBirthDate))
    {
      var relationshipDate = relative.GetRelationshipDate(personBirthDate);
      var date = _DateFormatter.ToString(relationshipDate);
      relation.Add(date);
    }

    var caption = string.Join(' ', relation);
    var href = PersonHref(relative.Id);
    var avatar = await RenderAvatarAsync(site, relative.MainPhoto);
    var name = _NameFormatter.ToString(relative, NameFormat.CommonPersonName);
    var dates = _LifeDatesFormatter.ToString(relative, showDeathDate: true, showAge: true);
    return RelativeItemTemplate.Fill(("href", href), ("avatar", avatar), ("relation", caption), ("name", name), ("dates", dates));
  }

  private static async Task<HtmlContent> RenderBiographyAsync(Site site, string biography)
  {
    if (string.IsNullOrWhiteSpace(biography))
      return HtmlContent.Empty;

    var rendered = await RenderMarkdownAsync(site, biography);
    var prose = ProseTemplate.Fill(("content", rendered));
    return RenderSection(UIStrings.FieldPersonBiography, prose);
  }

  private static async Task<HtmlContent> RenderAttachmentsAsync(Site site, AttachmentInfo[] attachments)
  {
    if (attachments.Length == 0)
      return HtmlContent.Empty;

    var items = new List<HtmlContent>();
    foreach (var attachment in attachments)
    {
      var media = await site.WriteMediaAsync(attachment.Data);
      var fileName = attachment.ShowFileName
        ? AttachmentFileNameTemplate.Fill(("fileName", attachment.FileName))
        : HtmlContent.Empty;
      var item = AttachmentItemTemplate.Fill(("href", media.Href), ("name", attachment.DisplayName), ("fileName", fileName));
      items.Add(item);
    }
    var list = RenderList("files", items);
    return RenderSection(UIStrings.FieldPersonAttachments, list);
  }

  // Read with MarkdownView's pipeline, then stripped of what the app never renders: raw HTML tags (their
  // text stays), attribute blocks, and links to anything but a page, a media file or a safe scheme.
  private static async Task<HtmlContent> RenderMarkdownAsync(Site site, string markdown)
  {
    var document = Markdown.Parse(markdown, BiographyMarkdown.Pipeline);
    foreach (var node in document.Descendants())
    {
      node.TryGetAttributes()?.Properties?.Clear();
    }
    foreach (var tag in document.Descendants<HtmlInline>().ToArray())
    {
      tag.Remove();
    }
    foreach (var autolink in document.Descendants<AutolinkInline>().ToArray())
    {
      if (!autolink.IsEmail && !IsSafe(autolink.Url))
      {
        var text = new LiteralInline(autolink.Url);
        autolink.ReplaceBy(text);
      }
    }
    foreach (var link in document.Descendants<LinkInline>().ToArray())
    {
      await RewriteLinkAsync(site, link);
    }
    var html = document.ToHtml(BiographyMarkdown.Pipeline);
    return HtmlContent.Raw(html);
  }

  private static bool IsSafe(string url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var uri) && SafeSchemes.Contains(uri.Scheme);

  private static async Task RewriteLinkAsync(Site site, LinkInline link)
  {
    var target = await ResolveLinkAsync(site, link.Url);
    if (!link.IsImage)
    {
      if (target is null)
      {
        Unwrap(link);
        return;
      }
      link.Url = target.Href;
      return;
    }

    // As in the app, an unresolved image renders as nothing rather than a broken picture.
    if (target is not { IsImage: true })
    {
      link.Remove();
      return;
    }

    link.Url = target.Href;
    var (widthPercent, caption) = BiographyMarkdown.DescriptionOf(link);
    if (widthPercent is null)
      return;

    foreach (var child in link.ToArray())
    {
      child.Remove();
    }
    var text = new LiteralInline(caption);
    link.AppendChild(text);
    if (target.PixelSize is { } size)
    {
      // MarkdownView's rule, min(pixel width, column) * percent, split across the two CSS limits.
      var width = (size.Width * widthPercent.Value / 100).ToString(CultureInfo.InvariantCulture);
      link.GetAttributes().AddProperty("style", $"width:{width}px;max-width:{widthPercent}%");
    }
  }

  private static void Unwrap(LinkInline link)
  {
    foreach (var child in link.ToArray())
    {
      child.Remove();
      link.InsertBefore(child);
    }
    link.Remove();
  }

  private static async Task<SiteLink?> ResolveLinkAsync(Site site, string? url)
  {
    if (url is null)
      return null;

    if (MarkdownLinkUtils.TryParsePersonId(url, out var personId))
    {
      var href = PersonHref(personId);
      return site.HasPerson(personId) ? new SiteLink(href, IsImage: false, PixelSize: null) : null;
    }

    if (MarkdownLinkUtils.TryParseMediaId(url, out var mediaId) || MarkdownLinkUtils.TryParseAttachmentId(url, out mediaId))
      return await site.WriteLinkedMediaAsync(mediaId);

    return IsSafe(url) ? new SiteLink(url, IsImage: true, PixelSize: null) : null;
  }

  private sealed record SiteLink(string Href, bool IsImage, Size? PixelSize);

  private sealed class Site(ZipArchive archive, IProjectDocument document, string projectName, HashSet<int> personIds, CancellationToken token)
  {
    private readonly Dictionary<int, SiteLink> _Media = [];

    public IProjectDocument Document => document;

    public string ProjectName => projectName;

    public CancellationToken Token => token;

    public bool HasPerson(int personId) => personIds.Contains(personId);

    // A zip in Create mode allows one open entry at a time, so a page is written only once fully rendered.
    public Task WriteTextAsync(string path, string text)
    {
      var bytes = Encoding.UTF8.GetBytes(text);
      return WriteAsync(path, bytes);
    }

    // Shared media arrives more than once, and a zip entry created twice extracts as a broken archive.
    public async Task<SiteLink> WriteMediaAsync(Data data)
    {
      if (_Media.TryGetValue(data.Id, out var written))
        return written;

      var bytes = data.Category.IsEnveloped() ? GedcomPhotoResidue.ExtractImageBytes(data.Content) : data.Content;
      var form = GedcomMedia.ResolveForm(data.MimeType, bytes);
      var extension = form is null ? string.Empty : "." + form;
      var fileName = data.Category.IsAttachment() ? await GedcomPhotoResidue.ExtractFileNameAsync(data, token) : null;
      var leaf = fileName is null ? "photo" + extension : SanitizeLeaf(fileName, "attachment" + extension);
      string[] segments = [MediaFolder, data.Id.ToString(CultureInfo.InvariantCulture), leaf];
      var path = string.Join('/', segments);
      await WriteAsync(path, bytes);

      var href = string.Join('/', segments.Select(Uri.EscapeDataString));
      var isImage = data.IsInlineImage();
      var pixelSize = isImage ? ImageUtils.PixelSize(bytes) : null;
      var link = new SiteLink(href, isImage, pixelSize);
      _Media[data.Id] = link;
      return link;
    }

    // A biography can link any photo or attachment in the project, not only its own person's.
    public async Task<SiteLink?> WriteLinkedMediaAsync(int dataId)
    {
      var data = await document.Data.TryGetDataByIdAsync(dataId, token);
      var isMedia = data is not null && (data.Category.IsPhoto() || data.Category.IsAttachment());
      return isMedia ? await WriteMediaAsync(data!) : null;
    }

    // A stored filename can be a full path from the machine that wrote it; only its last segment names the file.
    private static string SanitizeLeaf(string fileName, string fallback)
    {
      var leaf = fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];
      return FileNameUtils.Sanitize(leaf, fallback);
    }

    private async Task WriteAsync(string path, byte[] content)
    {
      var entry = archive.CreateEntry(path);
      await using var stream = entry.Open();
      await stream.WriteAsync(content, token);
    }
  }
}
