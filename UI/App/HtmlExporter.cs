using GT4.Core.Gedcom;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Pages;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace GT4.UI;

/// <summary>
/// Renders the whole project as a static site packed into one zip: an index over families and persons, a
/// page per family and per person, and every photo and attachment as a file of its own. Names, dates and
/// relatives go through the same formatters and display rules as the screens the pages mirror, so an
/// exported page reads the way the app does.
/// </summary>
public sealed class HtmlExporter
{
  private const string IndexPage = "index.html";
  private const string StyleSheet = "style.css";
  private const string MediaFolder = "media";
  private const string Css = """
    body { font-family: system-ui, sans-serif; line-height: 1.5; max-width: 60rem; margin: 0 auto; padding: 1rem; }
    nav { margin-bottom: 1rem; }
    img { max-width: 100%; height: auto; }
    figure { display: inline-block; margin: 0 1rem 1rem 0; vertical-align: top; }
    figure img { max-height: 25rem; }
    dt { font-weight: bold; }
    .subtitle, .dates, .file { opacity: 0.7; }
    @media (prefers-color-scheme: dark) { body { background: #1e1e1e; color: #e6e6e6; } a { color: #8cb4ff; } }
    """;

  // Everything else a biography links to -- javascript:, data:, file:, a relative path -- renders as text.
  private static readonly string[] SafeSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto, "tel"];

  private readonly INameFormatter _NameFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly DataConverterResolver _DataConverterResolver;
  private readonly IComparer<Name> _NameComparer;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IComparer<PersonInfo> _PersonInfoComparerByShortNames;

  public HtmlExporter(
    INameFormatter nameFormatter,
    IDateFormatter dateFormatter,
    IDateSpanFormatter dateSpanFormatter,
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
    _RelationshipTypeFormatter = relationshipTypeFormatter;
    _DataConverterResolver = dataConverterResolver;
    _NameComparer = nameComparer;
    _PersonInfoComparer = personInfoComparer;
    _PersonInfoComparerByShortNames = personInfoComparerByShortNames;
  }

  public async Task ExportAsync(IProjectDocument document, string projectName, Stream output, CancellationToken token)
  {
    using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
    var persons = await document.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, token);
    var families = await document.FamilyManager.GetFamiliesAsync(token);
    HashSet<int> personIds = [.. persons.Select(person => person.Id)];
    var site = new Site(archive, document, projectName, personIds, token);

    // Grouped the way ProjectPage groups its family cards, "No family" bucket included.
    var membersByNameId = persons
      .SelectMany(person => person.Names.Select(name => (NameId: name.Id, Person: person)))
      .ToLookup(x => x.NameId, x => x.Person);
    var familyMembers = families
      .OrderBy(family => family, _NameComparer)
      .Select(family => (Family: (Name)family, Members: membersByNameId[family.Id].ToArray()))
      .ToList();
    PersonInfo[] familyless = [.. persons.Where(FamilyInfoItem.HasNoFamily)];
    if (familyless.Length > 0)
    {
      familyMembers.Add((FamilyInfoItem.NoFamilyName, familyless));
    }

    Name[] indexedFamilies = [.. familyMembers.Select(f => f.Family)];
    var index = RenderIndex(site, indexedFamilies, persons);
    await site.WriteTextAsync(StyleSheet, Css);
    await site.WriteTextAsync(IndexPage, index);
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

  private static void AppendNavigation(HtmlBuilder html, Site site, IEnumerable<Name> families)
  {
    html.Raw("<nav>");
    html.Link(IndexPage, site.ProjectName);
    foreach (var family in families)
    {
      var href = FamilyHref(family.Id);
      html.Raw(" › ");
      html.Link(href, family.Value);
    }
    html.Raw("</nav>\n");
  }

  private string RenderIndex(Site site, Name[] families, PersonInfo[] persons)
  {
    var html = new HtmlBuilder();
    html.Element("h1", site.ProjectName);
    html.Element("h2", UIStrings.TitleFamiliesPage);
    html.Raw("<ul>\n");
    foreach (var family in families)
    {
      var href = FamilyHref(family.Id);
      html.Raw("<li>");
      html.Link(href, family.Value);
      html.Raw("</li>\n");
    }
    html.Raw("</ul>\n");
    // Common names, not the family cards' short ones: outside a family, two Annes are no longer told apart.
    html.Element("h2", UIStrings.LblPersons);
    AppendPersonList(html, persons, NameFormat.CommonPersonName, _PersonInfoComparer);
    return html.ToDocument(site.ProjectName);
  }

  private void AppendPersonList(HtmlBuilder html, PersonInfo[] persons, NameFormat nameFormat, IComparer<PersonInfo> comparer)
  {
    html.Raw("<ul>\n");
    foreach (var person in persons.OrderBy(person => person, comparer))
    {
      var href = PersonHref(person.Id);
      var name = _NameFormatter.ToString(person, nameFormat);
      var dates = PersonInfoView.FormatLifeDates(person, showDeathDate: true, showAge: true, _DateFormatter, _DateSpanFormatter);
      html.Raw("<li>");
      html.Link(href, name);
      html.Raw(" ");
      html.Element("span", dates, "dates");
      html.Raw("</li>\n");
    }
    html.Raw("</ul>\n");
  }

  private async Task<string> RenderFamilyAsync(Site site, Name family, PersonInfo[] members)
  {
    // The "No family" bucket has no row of its own, so it has no media either -- as on FamilyPage.
    var info = family.Id == FamilyInfoItem.NoFamilyName.Id
      ? new FamilyFullInfo(family, null, [], [])
      : await site.Document.FamilyManager.GetFamilyFullInfoAsync(family, site.Token);
    var attachments = await ReadAttachmentsAsync(info.Attachments, site.Token);

    var html = new HtmlBuilder();
    AppendNavigation(html, site, []);
    html.Element("h1", family.Value);
    await AppendPhotosAsync(html, site, info.MainPhoto, info.AdditionalPhotos);
    await AppendAttachmentsAsync(html, site, attachments);
    AppendPersonList(html, members, NameFormat.ShortPersonName, _PersonInfoComparerByShortNames);
    return html.ToDocument(family.Value);
  }

  // Gathers what PersonPage.GetPersonDataAsync does, in the same order, so the sections match the page's.
  private async Task<string> RenderPersonAsync(Site site, PersonInfo person)
  {
    var project = site.Document;
    var token = site.Token;
    var full = await project.PersonManager.GetPersonFullInfoAsync(person, token);
    var parents = await project.RelativesProvider.GetParentsAsync(full.RelativeInfos, token);
    var stepChildren = await project.RelativesProvider.GetStepChildrenAsync(full.RelativeInfos, token);
    var siblings = project.RelativesProvider.GetSiblings(full, parents);
    var roots = PersonPage.AssembleRoots(full, parents, siblings, stepChildren, project.RelativesProvider);
    var attachments = await ReadAttachmentsAsync(full.Attachments, token);
    var bio = await _DataConverterResolver(DataCategory.PersonBio).ToObjectAsync(full.Biography, token);
    var gedcomDetails = await _DataConverterResolver(DataCategory.PersonGedcomTags).ToObjectAsync(full.GedcomData, token);
    var familyDetails = await PersonFamilyDetails.ReadAsync(project, full, attachments, _NameFormatter, token);
    var biography = PersonPage.CombineBiography(bio as string, gedcomDetails as string, familyDetails);

    var shortName = _NameFormatter.ToString(full, NameFormat.ShortPersonName);
    var fullName = _NameFormatter.ToString(full, NameFormat.FullPersonName);
    var families = full.Names.Where(name => name.Type.HasFlag(NameType.FamilyName)).DefaultIfEmpty(FamilyInfoItem.NoFamilyName);
    var html = new HtmlBuilder();
    AppendNavigation(html, site, families);
    html.Element("h1", shortName);
    html.Element("p", fullName, "subtitle");
    AppendDates(html, full);
    await AppendPhotosAsync(html, site, full.MainPhoto, full.AdditionalPhotos);
    AppendRelatives(html, roots, full.BirthDate);
    if (!string.IsNullOrWhiteSpace(biography))
    {
      var rendered = await RenderMarkdownAsync(site, biography);
      html.Element("h2", UIStrings.FieldPersonBiography);
      html.Raw(rendered);
    }
    await AppendAttachmentsAsync(html, site, attachments);
    return html.ToDocument(shortName);
  }

  // Built directly rather than through the attachment converter, which would also decode every image
  // attachment into an ImageSource held by the app's image cache -- nothing a page written to disk needs.
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

  private void AppendDates(HtmlBuilder html, PersonFullInfo person)
  {
    var birthDate = _DateFormatter.ToString(person.BirthDate);
    var span = person.DeathDate.GetValueOrDefault(Date.Now) - person.BirthDate;
    var age = _DateSpanFormatter.ToString(span);
    html.Raw("<dl>\n");
    html.Field(UIStrings.FieldDateOfBirth, birthDate);
    if (person.DeathDate.HasValue)
    {
      var deathDate = _DateFormatter.ToString(person.DeathDate);
      html.Field(UIStrings.FieldDateOfDeath, deathDate);
    }
    html.Field(UIStrings.FieldAge, age);
    html.Raw("</dl>\n");
  }

  private static async Task AppendPhotosAsync(HtmlBuilder html, Site site, Data? mainPhoto, Data[] additionalPhotos)
  {
    Data[] photos = mainPhoto is null ? additionalPhotos : [mainPhoto, .. additionalPhotos];
    foreach (var photo in photos)
    {
      var media = await site.WriteMediaAsync(photo);
      var caption = photo.Category.IsTaggedPhoto() ? await GedcomPhotoResidue.ExtractTitleAsync(photo, site.Token) : null;
      html.Raw("<figure>");
      html.Image(media.Href, caption);
      if (!string.IsNullOrWhiteSpace(caption))
      {
        html.Element("figcaption", caption);
      }
      html.Raw("</figure>\n");
    }
  }

  // The first level of PersonPage's relatives tree, as it shows before anything is expanded.
  private void AppendRelatives(HtmlBuilder html, RelativeInfo[] relatives, Date personBirthDate)
  {
    if (relatives.Length == 0)
      return;

    html.Element("h2", UIStrings.LblRelatives);
    html.Raw("<ul>\n");
    foreach (var relative in relatives)
    {
      var relation = _RelationshipTypeFormatter.ToString(relative.Type, relative.BiologicalSex, relative.Generation, relative.Consanguinity);
      var bloodShare = RelativeInfoView.FormatBloodShare(relative);
      var href = PersonHref(relative.Id);
      var name = _NameFormatter.ToString(relative, NameFormat.CommonPersonName);
      var dates = PersonInfoView.FormatLifeDates(relative, showDeathDate: true, showAge: true, _DateFormatter, _DateSpanFormatter);
      html.Raw("<li>");
      html.Text(relation);
      if (bloodShare.Length > 0)
      {
        html.Raw(" ");
        html.Text(bloodShare);
      }
      if (RelativeInfoView.ShowsRelationshipDate(relative, personBirthDate))
      {
        var relationshipDate = RelativeInfoView.RelationshipDateOf(relative, personBirthDate);
        var date = _DateFormatter.ToString(relationshipDate);
        html.Raw(" ");
        html.Text(date);
      }
      html.Raw("<br>");
      html.Link(href, name);
      html.Raw(" ");
      html.Element("span", dates, "dates");
      html.Raw("</li>\n");
    }
    html.Raw("</ul>\n");
  }

  private static async Task AppendAttachmentsAsync(HtmlBuilder html, Site site, AttachmentInfo[] attachments)
  {
    if (attachments.Length == 0)
      return;

    html.Element("h2", UIStrings.FieldPersonAttachments);
    html.Raw("<ul>\n");
    foreach (var attachment in attachments)
    {
      var media = await site.WriteMediaAsync(attachment.Data);
      html.Raw("<li>");
      html.Link(media.Href, attachment.DisplayName);
      if (attachment.ShowFileName)
      {
        html.Raw(" ");
        html.Element("span", attachment.FileName, "file");
      }
      html.Raw("</li>\n");
    }
    html.Raw("</ul>\n");
  }

  // Parsed with MarkdownView's own pipeline so a biography is read exactly as the app reads it, then
  // cleaned of whatever the app would not render before Markdig writes it out: raw HTML tags (the app
  // drops them and keeps the text they wrap), attribute blocks, and links to anything but a page, a media
  // file or a safe scheme.
  private static async Task<string> RenderMarkdownAsync(Site site, string markdown)
  {
    var document = Markdown.Parse(markdown, MarkdownView.Pipeline);
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
    return document.ToHtml(MarkdownView.Pipeline);
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

    // An image nothing answers for renders as nothing in the app, not as a broken picture.
    if (target is not { IsImage: true })
    {
      link.Remove();
      return;
    }

    link.Url = target.Href;
    var (widthPercent, caption) = MarkdownView.DescriptionOf(link);
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

  // Every piece of text goes through here encoded; only the markup this class writes itself is raw.
  private sealed class HtmlBuilder
  {
    private readonly StringBuilder _Html = new();

    public void Raw(string html) => _Html.Append(html);

    public void Text(string? text) => _Html.Append(WebUtility.HtmlEncode(text));

    public void Element(string tag, string? text, string? cssClass = null)
    {
      _Html.Append('<').Append(tag);
      if (cssClass is not null)
      {
        _Html.Append(" class=\"").Append(cssClass).Append('"');
      }
      _Html.Append('>');
      Text(text);
      _Html.Append("</").Append(tag).Append(">\n");
    }

    public void Link(string href, string? text)
    {
      _Html.Append("<a href=\"").Append(href).Append("\">");
      Text(text);
      _Html.Append("</a>");
    }

    public void Image(string src, string? alt)
    {
      _Html.Append("<img src=\"").Append(src).Append("\" alt=\"");
      Text(alt);
      _Html.Append("\">");
    }

    public void Field(string name, string value)
    {
      Element("dt", name);
      Element("dd", value);
    }

    public string ToDocument(string title)
    {
      var encodedTitle = WebUtility.HtmlEncode(title);
      return $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{encodedTitle}}</title>
        <link rel="stylesheet" href="{{StyleSheet}}">
        </head>
        <body>
        {{_Html}}
        </body>
        </html>
        """;
    }
  }

  // One export's output: the zip, and the media already written into it.
  private sealed class Site(ZipArchive archive, IProjectDocument document, string projectName, HashSet<int> personIds, CancellationToken token)
  {
    private readonly Dictionary<int, SiteLink> _Media = [];

    public IProjectDocument Document => document;

    public string ProjectName => projectName;

    public CancellationToken Token => token;

    public bool HasPerson(int personId) => personIds.Contains(personId);

    // A zip in Create mode allows one open entry at a time, so a page is rendered in full -- media it
    // links included -- before its own entry is created.
    public Task WriteTextAsync(string path, string text)
    {
      var bytes = Encoding.UTF8.GetBytes(text);
      return WriteAsync(path, bytes);
    }

    // Media owned by two persons, or linked from several biographies, arrives more than once; a zip entry
    // created twice under one name extracts as a broken archive, so each row is written only the first time.
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
