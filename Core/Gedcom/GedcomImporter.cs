using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using System.Text;

namespace GT4.Core.Gedcom;

internal sealed class GedcomImporter : IGedcomImporter
{
  private const string ResidueMimeType = "application/x-gedcom";

  public async Task ImportAsync(IProjectDocument document, TextReader reader, CancellationToken token, string? mediaBasePath = null)
  {
    var records = await GedcomReader.ReadAsync(reader, token);
    var notesByXref = records
      .Where(r => r.Tag == GedcomTags.Note && r.Xref is not null)
      .ToDictionary(r => r.Xref!, r => r.Value);

    var individuals = records.Where(r => r.Tag == GedcomTags.Individual).ToArray();
    var families = records.Where(r => r.Tag == GedcomTags.Family).ToArray();

    // A child's tie to a family is adoptive when its FAMC link carries "PEDI adopted"; that lives on the
    // individual, so it is collected up front and consulted while wiring each family's children.
    var adoptedLinks = CollectAdoptedLinks(individuals);

    // Merge support: an import may land in a populated project. Existing names are reused so the
    // UNIQUE(Value, Type, ParentId) index never throws, and an incoming individual that matches an
    // existing person (same first name, family name and birth date, where an absent or unknown birth
    // date matches another absent or unknown one) is folded into that person instead of duplicated.
    // Every read below runs before the write transaction so the parallel reads inside
    // GetPersonFullInfoAsync never share the flow-affine import transaction.
    var existingNames = await document.Names.GetNamesByTypeAsync(NameType.AllNames, token);
    var existingPersons = await document.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, token);
    var matches = await ResolveMatchesAsync(document, individuals, existingPersons, token);
    var existingEdges = await CollectExistingEdgesAsync(document, matches.Values, token);

    // One outer transaction on a single flow: every inner Add* collapses to a SAVEPOINT and the lone
    // root commit stamps the revision, so the import lands all-or-nothing. The two passes stay strictly
    // sequential because a flow-affine transaction cannot be shared across parallel branches.
    using var transaction = await document.BeginTransactionAsync(token);

    var nameCache = existingNames.ToDictionary(n => (n.Value, n.Type, n.ParentId), n => n);
    var personByXref = new Dictionary<string, Person>();

    foreach (var individual in individuals)
    {
      Person person;
      if (individual.Xref is not null && matches.TryGetValue(individual.Xref, out var match))
      {
        person = match.Existing;
        await GapFillAsync(document, match, individual, notesByXref, mediaBasePath, token);
      }
      else
      {
        person = await ImportIndividualAsync(document, individual, notesByXref, nameCache, mediaBasePath, token);
      }

      if (individual.Xref is not null)
      {
        personByXref[individual.Xref] = person;
      }
    }

    foreach (var family in families)
    {
      await ImportFamilyAsync(document, family, personByXref, adoptedLinks, existingEdges, token);
    }

    await ImportPassthroughRecordsAsync(document, records, token);

    await transaction.CommitAsync(token);
  }

  // A reused person: the existing record an incoming individual was matched to, plus its full info so
  // gap-fill can tell which fields are empty without re-reading inside the transaction.
  private sealed record Match(PersonInfo Existing, PersonFullInfo Full);

  // The identity an incoming individual and an existing person are matched on. Built whenever a first
  // name is present; an absent or unknown birth date is carried as null, so an undated person matches
  // only another undated person and never folds into a dated one.
  private readonly record struct PersonIdentity(string FirstName, string? FamilyName, Date? BirthDate);

  /// <summary>
  /// Decides which incoming individuals fold into an existing person, keyed on first name, family name
  /// and birth date (undated matches only undated); anything ambiguous on either side imports as new.
  /// </summary>
  private static async Task<Dictionary<string, Match>> ResolveMatchesAsync(
    IProjectDocument document,
    GedcomNode[] individuals,
    PersonInfo[] existingPersons,
    CancellationToken token)
  {
    var existingByIdentity = existingPersons
      .Select(person => (person, identity: ExistingIdentity(person)))
      .Where(x => x.identity is not null)
      .GroupBy(x => x.identity!.Value)
      .ToDictionary(group => group.Key, group => group.Select(x => x.person).ToArray());

    var incoming = individuals
      .Where(individual => individual.Xref is not null)
      .Select(individual => (xref: individual.Xref!, identity: IncomingIdentity(individual)))
      .Where(x => x.identity is not null)
      .ToArray();
    var incomingCounts = incoming
      .GroupBy(x => x.identity!.Value)
      .ToDictionary(group => group.Key, group => group.Count());

    var matches = new Dictionary<string, Match>();
    foreach (var (xref, identity) in incoming)
    {
      if (incomingCounts[identity!.Value] != 1)
        continue;
      if (!existingByIdentity.TryGetValue(identity.Value, out var candidates) || candidates.Length != 1)
        continue;

      var existing = candidates[0];
      var full = await document.PersonManager.GetPersonFullInfoAsync(existing, token);
      matches[xref] = new Match(existing, full);
    }

    return matches;
  }

  /// <summary>
  /// The relationship edges the reused persons already have, so wiring can skip inserting a duplicate.
  /// Explicit dedup is required because a child edge stores a null date, and SQLite treats null
  /// primary-key parts as distinct, so the PRIMARY KEY alone would not reject the duplicate.
  /// </summary>
  private static async Task<HashSet<(int Owner, int Relative, RelationshipType Type, Date? Date)>> CollectExistingEdgesAsync(
    IProjectDocument document,
    IEnumerable<Match> matches,
    CancellationToken token)
  {
    var edges = new HashSet<(int, int, RelationshipType, Date?)>();
    foreach (var match in matches)
    {
      var relatives = await document.Relatives.GetRelativesAsync(match.Existing, token);
      foreach (var relative in relatives)
      {
        edges.Add((match.Existing.Id, relative.Id, relative.Type, relative.Date));
      }
    }

    return edges;
  }

  /// <summary>Fills only the fields a matched person is missing; populated values are never overwritten.</summary>
  private static async Task GapFillAsync(
    IProjectDocument document,
    Match match,
    GedcomNode individual,
    IReadOnlyDictionary<string, string?> notesByXref,
    string? mediaBasePath,
    CancellationToken token)
  {
    var full = match.Full;
    var additions = new List<Data>();

    var photos = SelectPhotos(individual, mediaBasePath);
    // Photos are added only when the person has none; otherwise these OBJEs stay unconsumed and fall
    // through to residue instead of being dropped.
    var addingPhotos = full.MainPhoto is null && full.AdditionalPhotos.Length == 0;
    GedcomNode[] consumedNodes = addingPhotos ? photos.Select(p => p.Node).ToArray() : [];

    if (full.Biography is null)
    {
      var biography = BuildBiography(individual, notesByXref);
      if (biography is not null)
        additions.Add(biography);
    }

    var incomingResidue = BuildResidueRoots(individual, consumedNodes);
    if (full.GedcomData is null && incomingResidue.Count > 0)
    {
      additions.Add(ToResidueData(incomingResidue));
    }

    if (addingPhotos)
    {
      var (mainPhoto, additionalPhotos) = BuildPhotos(photos);
      if (mainPhoto is not null)
        additions.Add(mainPhoto);
      additions.AddRange(additionalPhotos);
    }

    if (additions.Count > 0)
    {
      await document.PersonData.AddPersonDataSetAsync(match.Existing, [.. additions], token);
    }

    if (full.GedcomData is not null && incomingResidue.Count > 0)
    {
      var merged = await MergeResidueAsync(full.GedcomData, incomingResidue, token);
      if (merged is not null)
        await document.PersonData.UpdatePersonDataAsync(match.Existing, merged, DataCategory.PersonGedcomTags, token);
    }

    if (full.DeathDate is null)
    {
      var death = ParseEventDate(individual, GedcomTags.Death);
      if (death is not null)
      {
        var updated = new Person(match.Existing.Id, match.Existing.BirthDate, death, match.Existing.BiologicalSex);
        await document.Persons.UpdatePersonAsync(updated, token);
      }
    }
  }

  private static PersonIdentity? ExistingIdentity(PersonInfo person)
  {
    var firstName = person.Names.FirstOrDefault(name => name.Type.HasFlag(NameType.FirstName))?.Value;
    var familyName = person.Names.FirstOrDefault(name => name.Type.HasFlag(NameType.FamilyName))?.Value;
    return ToIdentity(firstName, familyName, person.BirthDate);
  }

  private static PersonIdentity? IncomingIdentity(GedcomNode individual)
  {
    var birthDate = ParseEventDate(individual, GedcomTags.Birth);
    var (firstName, familyName) = RawNameParts(individual);
    return ToIdentity(firstName, familyName, birthDate);
  }

  private static PersonIdentity? ToIdentity(string? firstName, string? familyName, Date? birthDate)
  {
    if (string.IsNullOrWhiteSpace(firstName))
      return null;

    var family = string.IsNullOrWhiteSpace(familyName) ? null : familyName;
    var birth = birthDate is { Status: not DateStatus.Unknown } ? birthDate : null;
    return new PersonIdentity(firstName, family, birth);
  }

  /// <summary>
  /// The first given token and the surname straight from the GEDCOM NAME, matching how
  /// <see cref="BuildNamesAsync"/> stores the first name and family name, so the identity computed here
  /// lines up with the one read back off an existing person.
  /// </summary>
  private static (string? First, string? Family) RawNameParts(GedcomNode individual)
  {
    var nameNode = individual.Child(GedcomTags.Name);
    if (nameNode is null)
      return (null, null);

    var given = nameNode.ChildValue(GedcomTags.Given) ?? GivenFromValue(nameNode.Value);
    var surname = nameNode.ChildValue(GedcomTags.Surname) ?? SurnameFromValue(nameNode.Value);
    var first = given?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    return (first, surname?.Trim());
  }

  /// <summary>
  /// Preserves the unmodeled top-level records (submitter/submission/source/repository) verbatim in the
  /// Metadata table so they survive a round-trip even though GT4 has no schema for them. See
  /// <see cref="GedcomMetadata"/> for the keying and the references that are intentionally not preserved.
  /// </summary>
  private static async Task ImportPassthroughRecordsAsync(IProjectDocument document, GedcomNode[] records, CancellationToken token)
  {
    foreach (var record in records.Where(GedcomMetadata.IsPassthrough))
    {
      var writer = new StringWriter();
      GedcomWriter.Write(writer, record);
      await document.Metadata.AddAsync(GedcomMetadata.Key(record), writer.ToString(), token);
    }
  }

  private static HashSet<(string Child, string Family)> CollectAdoptedLinks(IEnumerable<GedcomNode> individuals)
  {
    return individuals
      .Where(individual => individual.Xref is not null)
      .SelectMany(individual => individual
        .ChildrenWithTag(GedcomTags.FamilyChild)
        .Where(familyChild => familyChild.Value is not null && IsAdoptedLink(familyChild))
        .Select(familyChild => (Child: individual.Xref!, Family: familyChild.Value!)))
      .ToHashSet();
  }

  private static bool IsAdoptedLink(GedcomNode familyChild)
  {
    var pedigree = familyChild.ChildValue(GedcomTags.Pedigree);
    return string.Equals(pedigree, GedcomTags.AdoptedPedigree, StringComparison.OrdinalIgnoreCase);
  }

  private static async Task<Person> ImportIndividualAsync(
    IProjectDocument document,
    GedcomNode individual,
    IReadOnlyDictionary<string, string?> notesByXref,
    Dictionary<(string, NameType, int?), Name> nameCache,
    string? mediaBasePath,
    CancellationToken token)
  {
    var sex = GedcomMapping.ParseSex(individual.ChildValue(GedcomTags.Sex));
    var names = await BuildNamesAsync(document, individual, sex, nameCache, token);
    var biography = BuildBiography(individual, notesByXref);
    var photos = SelectPhotos(individual, mediaBasePath);
    var photoNodes = photos.Select(p => p.Node).ToArray();
    var (mainPhoto, additionalPhotos) = BuildPhotos(photos);

    var toAdd = PersonFullInfo.Empty with
    {
      BirthDate = ParseEventDate(individual, GedcomTags.Birth) ?? new Date { Status = DateStatus.Unknown },
      DeathDate = ParseEventDate(individual, GedcomTags.Death),
      BiologicalSex = sex,
      Names = names,
      MainPhoto = mainPhoto,
      AdditionalPhotos = additionalPhotos,
      Biography = biography,
      GedcomData = BuildResidueData(individual, photoNodes),
    };

    var added = await document.PersonManager.AddPersonAsync(toAdd, token);
    return new Person(added.Id, added.BirthDate, added.DeathDate, added.BiologicalSex);
  }

  /// <summary>
  /// Captures everything GT4 does not model as one verbatim GEDCOM blob: each fully-unmodeled direct INDI
  /// child (<c>OCCU</c>, <c>RESI</c>, <c>EVEN</c>, ...) whole, plus the unmodeled sub-tags of the owned tags
  /// GT4 reads only part of (a <c>BIRT</c>'s <c>PLAC</c>/<c>SOUR</c>, a <c>NAME</c>'s <c>NICK</c>, ...) as a
  /// residual copy of that tag. Carried on <see cref="PersonFullInfo.GedcomData"/>, the blob is stored with
  /// the person and merged back on export, so the round-trip loses nothing in its original document order.
  /// </summary>
  private static Data? BuildResidueData(GedcomNode individual, GedcomNode[] consumedPhotoNodes)
  {
    var roots = BuildResidueRoots(individual, consumedPhotoNodes);
    return roots.Count == 0 ? null : ToResidueData(roots);
  }

  /// <summary>
  /// The residue roots of an individual: each fully-unmodeled INDI child whole, the first occurrence of an
  /// owned tag reduced to a residual copy of just its unmodeled sub-tags, and every later occurrence of an
  /// owned tag whole. GT4's readers all take <c>Child(tag)</c>, so only the first occurrence is consumed into
  /// the model; a repeated <c>NAME</c>/<c>NOTE</c> keeps its value here and is re-emitted standalone on export.
  /// </summary>
  private static List<GedcomNode> BuildResidueRoots(GedcomNode individual, GedcomNode[] consumedPhotoNodes)
  {
    var roots = new List<GedcomNode>();
    var consumedOwned = new HashSet<string>();
    foreach (var child in individual.Children)
    {
      // OBJEs consumed into the person's photo set by BuildPhotos are excluded here to avoid storing them a
      // second time as opaque residue. An OBJE that was not consumed — unreadable, or skipped because the
      // person already had a photo — is absent from this set and so survives verbatim as residue.
      // FullyOwnedTags are regenerated from the edge graph, so neither they nor their sub-tags are preserved.
      if (consumedPhotoNodes.Contains(child) || GedcomMapping.FullyOwnedTags.Contains(child.Tag))
        continue;

      if (GedcomMapping.OwnedTagModeledChildren.TryGetValue(child.Tag, out var modeled) && consumedOwned.Add(child.Tag))
      {
        var residual = ResidualNode(child, modeled);
        if (residual is not null)
          roots.Add(residual);
      }
      else
      {
        roots.Add(child);
      }
    }
    return roots;
  }

  private static Data ToResidueData(IEnumerable<GedcomNode> roots)
  {
    var content = Encoding.UTF8.GetBytes(SerializeRoots(roots));
    return new Data(ElementId.NonCommittedId, content, ResidueMimeType, DataCategory.PersonGedcomTags);
  }

  private static string SerializeRoots(IEnumerable<GedcomNode> roots) => string.Concat(roots.Select(Serialize));

  private static string Serialize(GedcomNode root)
  {
    var writer = new StringWriter();
    GedcomWriter.Write(writer, root);
    return writer.ToString();
  }

  /// <summary>
  /// Folds the incoming residue roots a matched person does not already carry into their existing residue
  /// blob, comparing <see cref="GedcomWriter"/>-serialized roots on both sides so reader/writer normalization
  /// never makes equal content look new. Returns null when nothing new survives, so a re-import writes nothing.
  /// </summary>
  private static async Task<Data?> MergeResidueAsync(Data existing, List<GedcomNode> incoming, CancellationToken token)
  {
    var existingText = Encoding.UTF8.GetString(existing.Content);
    var existingRoots = await GedcomReader.ReadAsync(new StringReader(existingText), token);
    var seen = existingRoots.Select(Serialize).ToHashSet();

    var added = incoming.Where(root => seen.Add(Serialize(root))).ToArray();
    if (added.Length == 0)
      return null;

    return ToResidueData([.. existingRoots, .. added]);
  }

  /// <summary>
  /// A copy of an owned node carrying only the children GT4 does not model — the node's value and modeled
  /// sub-tags are dropped since those ride in the GT4 model — or <c>null</c> when nothing unmodeled remains
  /// (a DATE-only BIRT yields no residue). The source node is never mutated: <see cref="ParseEventDate"/>
  /// and <see cref="BuildNamesAsync"/> still read the live DATE/GIVN/SURN off it.
  /// </summary>
  private static GedcomNode? ResidualNode(GedcomNode owned, HashSet<string> modeled)
  {
    var residual = new GedcomNode { Tag = owned.Tag };
    foreach (var child in owned.Children)
    {
      if (!modeled.Contains(child.Tag))
        residual.Add(child);
    }
    return residual.Children.Count > 0 ? residual : null;
  }

  // A photo materialized once from an OBJE: an embedded BLOB decoded, or an external image FILE read off
  // disk. Holding the bytes here means the "can this load?" decision is made at selection time, so an OBJE
  // whose bytes cannot be obtained is simply absent from the set and falls through to residue.
  // Residual is the OBJE's own unmodeled children (chiefly TITL), computed per-node rather than through
  // the shared OwnedTagModeledChildren/consumedOwned machinery: that machinery assumes one occurrence per
  // tag, but a person can have several OBJEs, each needing its own distinct residual tags preserved.
  private sealed record PhotoCandidate(GedcomNode Node, byte[] Content, string? MimeType, bool Primary, GedcomNode? Residual);

  // The OBJE sub-tags TryReadPhoto already reads; anything else (chiefly TITL) is residual.
  private static readonly HashSet<string> ObjeModeledChildren =
    [GedcomTags.Form, GedcomTags.Primary, GedcomTags.Blob, GedcomTags.File];

  /// <summary>
  /// The direct INDI <c>OBJE</c> photos GT4 can load, materialized to bytes in document order: an embedded
  /// base64 <c>BLOB</c>, or an external <c>FILE</c> image reference that resolves under
  /// <paramref name="mediaBasePath"/>. An OBJE whose bytes cannot be obtained — a non-image, an unfound or
  /// unreadable FILE, or a corrupt BLOB — is dropped from the set and survives as residue instead, so a
  /// single bad image never aborts the all-or-nothing import.
  /// </summary>
  private static PhotoCandidate[] SelectPhotos(GedcomNode individual, string? mediaBasePath) =>
    individual.Children
      .Select(o => TryReadPhoto(o, mediaBasePath))
      .Where(p => p is not null)
      .Select(p => p!)
      .ToArray();

  private static PhotoCandidate? TryReadPhoto(GedcomNode obje, string? mediaBasePath)
  {
    if (obje.Tag != GedcomTags.Object)
      return null;

    var residual = ResidualNode(obje, ObjeModeledChildren);

    if (IsEmbeddedPhoto(obje))
    {
      try
      {
        var blob = Convert.FromBase64String(obje.ChildValue(GedcomTags.Blob)!);
        var mimeType = GedcomMedia.ToMimeType(obje.ChildValue(GedcomTags.Form));
        return new PhotoCandidate(obje, blob, mimeType, IsPrimary(obje), residual);
      }
      catch (FormatException)
      {
        return null;
      }
    }

    var path = ExternalImagePath(obje, mediaBasePath);
    if (path is null)
      return null;

    try
    {
      var content = System.IO.File.ReadAllBytes(path);
      var mime = GedcomMedia.ToMimeType(MediaForm(obje));
      return new PhotoCandidate(obje, content, mime, IsPrimary(obje), residual);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
      return null;
    }
  }

  /// <summary>
  /// Builds the photo data from the materialized candidates. The one marked <c>_PRIM Y</c> — or the first
  /// when none is marked — becomes the main (profile) photo; the rest are additional.
  /// </summary>
  private static (Data? Main, Data[] Additional) BuildPhotos(PhotoCandidate[] photos)
  {
    if (photos.Length == 0)
      return (null, []);

    var mainPhoto = photos.FirstOrDefault(p => p.Primary) ?? photos[0];
    var main = BuildPhotoData(mainPhoto, DataCategory.PersonMainPhoto);
    var additional = photos
      .Where(p => !ReferenceEquals(p, mainPhoto))
      .Select(p => BuildPhotoData(p, DataCategory.PersonPhoto))
      .ToArray();
    return (main, additional);
  }

  private static Data BuildPhotoData(PhotoCandidate candidate, DataCategory plainCategory)
  {
    if (candidate.Residual is null)
      return new Data(ElementId.NonCommittedId, candidate.Content, candidate.MimeType, plainCategory);

    var content = GedcomPhotoResidue.Encode(candidate.Content, candidate.Residual);
    return new Data(ElementId.NonCommittedId, content, candidate.MimeType, plainCategory.AsTaggedPhoto());
  }

  private static bool IsEmbeddedPhoto(GedcomNode node) =>
    node.Tag == GedcomTags.Object && !string.IsNullOrWhiteSpace(node.ChildValue(GedcomTags.Blob));

  private static bool IsPrimary(GedcomNode obje) =>
    string.Equals(obje.ChildValue(GedcomTags.Primary), GedcomTags.PrimaryYes, StringComparison.OrdinalIgnoreCase);

  /// <summary>
  /// The on-disk path of an <c>OBJE</c>'s external image <c>FILE</c> when it resolves under
  /// <paramref name="mediaBasePath"/> and is an image, else null. A relative reference is taken relative to
  /// the base path; an absolute one is used as-is. Only image media is loaded, so a non-image FILE (a PDF
  /// scan) is left as residue.
  /// </summary>
  private static string? ExternalImagePath(GedcomNode obje, string? mediaBasePath)
  {
    if (mediaBasePath is null || obje.Tag != GedcomTags.Object)
      return null;

    var fileRef = obje.ChildValue(GedcomTags.File);
    if (string.IsNullOrWhiteSpace(fileRef) || !IsImageMedia(MediaForm(obje), fileRef))
      return null;

    var normalized = fileRef.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    var path = Path.IsPathRooted(normalized) ? normalized : Path.Combine(mediaBasePath, normalized);
    return System.IO.File.Exists(path) ? path : null;
  }

  // GEDCOM 5.5.1 nests FORM under FILE; the older 5.5 form sits directly under OBJE. Read either.
  private static string? MediaForm(GedcomNode obje) =>
    obje.Child(GedcomTags.File)?.ChildValue(GedcomTags.Form) ?? obje.ChildValue(GedcomTags.Form);

  // Image subtypes GT4 will load as a photo, matched against either the OBJE FORM token or the FILE
  // extension. A non-image FORM (e.g. "pdf") must not match: GedcomMedia.ToMimeType blindly prefixes
  // "image/" to any token, so the form is checked against this set rather than through that mapping.
  private static readonly HashSet<string> ImageForms =
    new(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "webp" };

  private static bool IsImageMedia(string? form, string fileRef)
  {
    var token = form?.Trim();
    if (token is not null && (ImageForms.Contains(token) || token.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
      return true;

    var extension = Path.GetExtension(fileRef).TrimStart('.');
    return ImageForms.Contains(extension);
  }

  /// <summary>
  /// The presence of the event tag — not just a parseable date — carries meaning: a bare <c>DEAT</c>
  /// means the person is known to be dead even when the date is unknown. So an event with no usable date
  /// still yields an <see cref="DateStatus.Unknown"/> date rather than <c>null</c>.
  /// </summary>
  private static Date? ParseEventDate(GedcomNode individual, string eventTag)
  {
    var eventNode = individual.Child(eventTag);
    if (eventNode is null)
      return null;

    return GedcomDate.Parse(eventNode.ChildValue(GedcomTags.Date));
  }

  private static async Task<Name[]> BuildNamesAsync(
    IProjectDocument document,
    GedcomNode individual,
    BiologicalSex sex,
    Dictionary<(string, NameType, int?), Name> nameCache,
    CancellationToken token)
  {
    var nameNode = individual.Child(GedcomTags.Name);
    if (nameNode is null)
      return [];

    var given = nameNode.ChildValue(GedcomTags.Given) ?? GivenFromValue(nameNode.Value);
    var surname = nameNode.ChildValue(GedcomTags.Surname) ?? SurnameFromValue(nameNode.Value);
    var declension = GedcomMapping.Declension(sex);

    var names = new List<Name>();
    if (!string.IsNullOrWhiteSpace(given))
    {
      // A multi-token given name ("Patrick Branwell Josef") has no single slot in GT4's model: the first
      // token is the first name and every later token becomes its own patronymic, the only second-given
      // slot, which is exactly where the exporter looks to rebuild the GIVN, so the split round-trips.
      var parts = given.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
      var firstName = await GetOrAddNameAsync(document, parts[0], NameType.FirstName | declension, null, nameCache, token);
      names.Add(firstName);
      foreach (var middle in parts.Skip(1))
      {
        var patronymic = await GetOrAddNameAsync(document, middle, NameType.Patronymic | declension, null, nameCache, token);
        names.Add(patronymic);
      }
    }
    if (!string.IsNullOrWhiteSpace(surname))
    {
      // Group people by surname into a GT4 family so they appear on the families page. The family name is
      // the bare surname; the declined last name lives under it (the same shape FamilyManager.AddFamilyAsync
      // builds) and is what the exporter reads back as the surname.
      var trimmed = surname.Trim();
      var family = await GetOrAddNameAsync(document, trimmed, NameType.FamilyName, null, nameCache, token);
      var lastName = await GetOrAddNameAsync(document, trimmed, NameType.LastName | declension, family, nameCache, token);
      names.Add(family);
      names.Add(lastName);
    }
    return [.. names];
  }

  private static async Task<Name> GetOrAddNameAsync(
    IProjectDocument document,
    string value,
    NameType type,
    Name? parent,
    Dictionary<(string, NameType, int?), Name> nameCache,
    CancellationToken token)
  {
    var key = (value, type, parent?.Id);
    if (nameCache.TryGetValue(key, out var cached))
      return cached;

    var name = await document.Names.AddNameAsync(value, type, parent, token);
    nameCache[key] = name;
    return name;
  }

  private static string? GivenFromValue(string? value)
  {
    if (string.IsNullOrEmpty(value))
      return null;

    var slash = value.IndexOf('/');
    return (slash < 0 ? value : value[..slash]).Trim();
  }

  private static string? SurnameFromValue(string? value)
  {
    if (string.IsNullOrEmpty(value))
      return null;

    var open = value.IndexOf('/');
    if (open < 0)
      return null;

    var close = value.IndexOf('/', open + 1);
    return close < 0 ? null : value[(open + 1)..close].Trim();
  }

  private static Data? BuildBiography(GedcomNode individual, IReadOnlyDictionary<string, string?> notesByXref)
  {
    var noteNode = individual.Child(GedcomTags.Note);
    if (noteNode is null)
      return null;

    var text = ResolveNote(noteNode.Value, notesByXref);
    if (string.IsNullOrEmpty(text))
      return null;

    var content = Encoding.UTF8.GetBytes(text);
    return new Data(ElementId.NonCommittedId, content, "text/plain", DataCategory.PersonBio);
  }

  private static string? ResolveNote(string? value, IReadOnlyDictionary<string, string?> notesByXref)
  {
    if (value is null)
      return null;

    var isPointer = value.Length >= 2 && value[0] == '@' && value[^1] == '@';
    return isPointer ? notesByXref.GetValueOrDefault(value) : value;
  }

  private static async Task ImportFamilyAsync(
    IProjectDocument document,
    GedcomNode family,
    IReadOnlyDictionary<string, Person> personByXref,
    HashSet<(string Child, string Family)> adoptedLinks,
    HashSet<(int Owner, int Relative, RelationshipType Type, Date? Date)> existingEdges,
    CancellationToken token)
  {
    var husband = Resolve(family.ChildValue(GedcomTags.Husband), personByXref);
    var wife = Resolve(family.ChildValue(GedcomTags.Wife), personByXref);
    var familyXref = family.Xref ?? string.Empty;
    var children = family
      .ChildrenWithTag(GedcomTags.Child)
      .Where(c => c.Value is not null)
      .Select(c => (Person: Resolve(c.Value, personByXref), Adopted: adoptedLinks.Contains((c.Value!, familyXref))))
      .Where(c => c.Person is not null)
      .Select(c => (Person: c.Person!, c.Adopted))
      .ToArray();

    // A MARR event is the marker of a real spouse edge: emit one spouse relationship per event (a couple
    // can have several, e.g. a remarriage). Co-parents with no MARR are intentionally left unmarried.
    if (husband is not null && wife is not null)
    {
      var marriages = family.ChildrenWithTag(GedcomTags.Marriage).ToArray();
      var spouses = marriages
        .Select(m => new Relative(wife, RelationshipType.Spouse, ParseSpouseDate(m)))
        .Where(s => !existingEdges.Contains((husband.Id, s.Id, s.Type, s.Date)))
        .ToArray();
      if (spouses.Length > 0)
      {
        await document.Relatives.AddRelativesAsync(husband, spouses, token);
      }
    }

    if (children.Length == 0)
      return;

    foreach (var parent in new[] { husband, wife })
    {
      if (parent is null)
        continue;

      var childRelatives = children
        .Select(c => new Relative(c.Person, c.Adopted ? RelationshipType.AdoptiveChild : RelationshipType.Child, null))
        .Where(r => !existingEdges.Contains((parent.Id, r.Id, r.Type, r.Date)))
        .ToArray();
      if (childRelatives.Length > 0)
      {
        await document.Relatives.AddRelativesAsync(parent, childRelatives, token);
      }
    }
  }

  private static Date? ParseSpouseDate(GedcomNode marriage)
  {
    var date = GedcomDate.Parse(marriage.ChildValue(GedcomTags.Date));
    return date.Status == DateStatus.Unknown ? null : date;
  }

  private static Person? Resolve(string? xref, IReadOnlyDictionary<string, Person> personByXref) =>
    xref is not null ? personByXref.GetValueOrDefault(xref) : null;
}
