using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using System.Text;

namespace GT4.Core.Gedcom;

internal sealed class GedcomExporter : IGedcomExporter
{
  /// <summary>
  /// A GEDCOM family reconstructed from GT4's pairwise edge graph: a husband/wife couple plus the
  /// children that share exactly this pair of parents and the marriage dates between the couple.
  /// </summary>
  private sealed class Family
  {
    public int? HusbandId { get; init; }
    public int? WifeId { get; init; }
    public List<(int Id, bool Adopted)> Children { get; } = [];
    public List<Date?> MarriageDates { get; } = [];
    public string Xref { get; set; } = string.Empty;
  }

  /// <summary>
  /// Everything one INDI needs, gathered per person so the builder reads each field from a single object
  /// instead of threading a parallel by-id lookup for biography, residue, photos and family links.
  /// </summary>
  private sealed record Individual(
    Person Person,
    PersonInfo? Info,
    Data? Biography,
    Data? Residue,
    Data? MainPhoto,
    Data[] AdditionalPhotos,
    Data[] Attachments,
    string[] SpouseFamilies,
    (string Xref, bool Adopted)[] ChildFamilies);

  public async Task ExportAsync(IProjectDocument document, TextWriter writer, CancellationToken token)
  {
    var persons = await document.Persons.GetPersonsAsync(token);
    var personById = persons.ToDictionary(p => p.Id);
    var personInfos = await document.PersonManager.GetPersonInfosAsync(persons, selectMainPhoto: false, token);
    var infoById = personInfos.ToDictionary(p => p.Id);
    var biographies = await document.PersonData.GetPersonDataSetAsync(persons, DataCategory.PersonBio, token);
    var residues = await document.PersonData.GetPersonDataSetAsync(persons, DataCategory.PersonGedcomTags, token);
    var mainPhotos = await document.PersonData.GetMergedPhotoSetAsync(persons, DataCategory.PersonMainPhoto, token);
    var additionalPhotos = await document.PersonData.GetMergedPhotoSetAsync(persons, DataCategory.PersonPhoto, token);
    var attachments = await document.PersonData.GetPersonDataSetAsync(persons, DataCategory.PersonAttachment, token);
    var relatives = await document.Relatives.GetRelativesForPersonsAsync(persons, token);

    var families = BuildFamilies(relatives, personById);
    var individuals = BuildIndividuals(persons, infoById, biographies, residues, mainPhotos, additionalPhotos, attachments, families);

    WriteHeader(writer);
    await WriteIndividualsAsync(writer, individuals, token);
    WriteFamilies(writer, families);
    await WritePassthroughRecordsAsync(document, writer, token);
    GedcomWriter.Write(writer, new GedcomNode { Tag = GedcomTags.Trailer });
  }

  /// <summary>
  /// Re-emits the unmodeled records (submitter/source/...) that import stashed verbatim in the Metadata
  /// table. They are already serialized GEDCOM text, so they are written through unchanged.
  /// </summary>
  private static async Task WritePassthroughRecordsAsync(IProjectDocument document, TextWriter writer, CancellationToken token)
  {
    var records = await document.Metadata.GetByPrefixAsync<string>(GedcomMetadata.KeyPrefix, token);
    foreach (var record in records)
    {
      writer.Write(record);
    }
  }

  private static Family[] BuildFamilies(Dictionary<int, Relative[]> relatives, Dictionary<int, Person> personById)
  {
    var edges = CollectEdges(relatives);

    var byKey = new Dictionary<(int? Husband, int? Wife), Family>();

    Family GetFamily(int[] parentIds)
    {
      var key = OrderParents(parentIds, personById);
      if (!byKey.TryGetValue(key, out var family))
      {
        family = new Family { HusbandId = key.Husband, WifeId = key.Wife };
        byKey[key] = family;
      }
      return family;
    }

    // Native and adoptive children of the same couple share one FAM; the per-child pedigree (recorded on
    // each child's FAMC link, not on the FAM) is what distinguishes them.
    AddChildren(edges.ParentEdges, adopted: false, GetFamily);
    AddChildren(edges.AdoptiveEdges, adopted: true, GetFamily);

    foreach (var (pair, dates) in edges.SpouseDates)
    {
      GetFamily([pair.Low, pair.High]).MarriageDates.AddRange(dates);
    }

    // Deterministic family order (and therefore @Fn@ numbering) for stable output and tests.
    var ordered = byKey.Values
      .OrderBy(f => f.HusbandId ?? int.MaxValue)
      .ThenBy(f => f.WifeId ?? int.MaxValue)
      .ToList();
    for (var i = 0; i < ordered.Count; i++)
    {
      ordered[i].Xref = $"@F{i + 1}@";
      ordered[i].Children.Sort((a, b) => a.Id.CompareTo(b.Id));
    }
    return [.. ordered];
  }

  private static void AddChildren(
    HashSet<(int Child, int Parent)> edges,
    bool adopted,
    Func<int[], Family> getFamily)
  {
    var childrenByParents = edges
      .GroupBy(e => e.Child)
      .ToDictionary(g => g.Key, g => g.Select(e => e.Parent).Distinct().ToArray());

    foreach (var (child, parents) in childrenByParents)
    {
      getFamily(parents).Children.Add((child, adopted));
    }
  }

  private static (HashSet<(int Child, int Parent)> ParentEdges,
                  HashSet<(int Child, int Parent)> AdoptiveEdges,
                  Dictionary<(int Low, int High), HashSet<Date?>> SpouseDates) CollectEdges(
    Dictionary<int, Relative[]> relatives)
  {
    var parentEdges = new HashSet<(int Child, int Parent)>();
    var adoptiveEdges = new HashSet<(int Child, int Parent)>();
    var spouseDates = new Dictionary<(int Low, int High), HashSet<Date?>>();

    foreach (var (ownerId, ownerRelatives) in relatives)
    {
      foreach (var relative in ownerRelatives)
      {
        switch (relative.Type)
        {
          // The same stored row is seen from both sides; the HashSet folds the duplicate away.
          case RelationshipType.Parent:
            parentEdges.Add((ownerId, relative.Id));
            break;
          case RelationshipType.Child:
            parentEdges.Add((relative.Id, ownerId));
            break;
          case RelationshipType.AdoptiveParent:
            adoptiveEdges.Add((ownerId, relative.Id));
            break;
          case RelationshipType.AdoptiveChild:
            adoptiveEdges.Add((relative.Id, ownerId));
            break;
          case RelationshipType.Spouse:
            var pair = ownerId < relative.Id ? (ownerId, relative.Id) : (relative.Id, ownerId);
            if (!spouseDates.TryGetValue(pair, out var dates))
            {
              spouseDates[pair] = dates = [];
            }
            dates.Add(relative.Date);
            break;
        }
      }
    }

    return (parentEdges, adoptiveEdges, spouseDates);
  }

  /// <summary>
  /// Assigns up to two parents to the husband/wife slots deterministically, so a couple derived from a
  /// child's parent-set and the same couple derived from a spouse edge always produce the identical key.
  /// Sex drives the slot (male before unknown before female); ties break by id.
  /// </summary>
  private static (int? Husband, int? Wife) OrderParents(int[] parentIds, Dictionary<int, Person> personById)
  {
    if (parentIds.Length == 1)
    {
      var only = personById[parentIds[0]];
      return only.BiologicalSex == BiologicalSex.Female ? (null, only.Id) : (only.Id, null);
    }

    var ordered = parentIds
      .Select(id => personById[id])
      .OrderBy(p => SexRank(p.BiologicalSex))
      .ThenBy(p => p.Id)
      .ToList();
    return (ordered[0].Id, ordered[1].Id);
  }

  private static int SexRank(BiologicalSex sex) => sex switch
  {
    BiologicalSex.Male => 0,
    BiologicalSex.Female => 2,
    _ => 1,
  };

  private static void WriteHeader(TextWriter writer)
  {
    var header = new GedcomNode { Tag = GedcomTags.Header };
    header.Add(new GedcomNode { Tag = GedcomTags.Source, Value = "GT4" });
    var gedc = new GedcomNode { Tag = GedcomTags.Gedcom };
    gedc.Add(new GedcomNode { Tag = GedcomTags.Version, Value = "5.5.1" });
    gedc.Add(new GedcomNode { Tag = GedcomTags.Form, Value = "LINEAGE-LINKED" });
    header.Add(gedc);
    header.Add(new GedcomNode { Tag = GedcomTags.Charset, Value = "UTF-8" });
    GedcomWriter.Write(writer, header);
  }

  private static async Task WriteIndividualsAsync(TextWriter writer, Individual[] individuals, CancellationToken token)
  {
    foreach (var individual in individuals)
    {
      var node = await BuildIndividualAsync(individual, token);
      GedcomWriter.Write(writer, node);
    }
  }

  private static Individual[] BuildIndividuals(
    Person[] persons,
    Dictionary<int, PersonInfo> infoById,
    Dictionary<int, Data[]> biographies,
    Dictionary<int, Data[]> residues,
    Dictionary<int, Data[]> mainPhotos,
    Dictionary<int, Data[]> additionalPhotos,
    Dictionary<int, Data[]> attachments,
    Family[] families)
  {
    var spouseFamilies = new Dictionary<int, List<string>>();
    var childFamilies = new Dictionary<int, List<(string Xref, bool Adopted)>>();
    foreach (var family in families)
    {
      LinkFamily(spouseFamilies, family.HusbandId, family.Xref);
      LinkFamily(spouseFamilies, family.WifeId, family.Xref);
      foreach (var (childId, adopted) in family.Children)
      {
        LinkChild(childFamilies, childId, family.Xref, adopted);
      }
    }

    var individuals = new List<Individual>();
    foreach (var person in persons.OrderBy(p => p.Id))
    {
      var info = infoById.GetValueOrDefault(person.Id);
      var biography = biographies.GetValueOrDefault(person.Id)?.FirstOrDefault();
      var residue = residues.GetValueOrDefault(person.Id)?.FirstOrDefault();
      var mainPhoto = mainPhotos.GetValueOrDefault(person.Id)?.FirstOrDefault();
      var morePhotos = additionalPhotos.GetValueOrDefault(person.Id) ?? [];
      var personAttachments = attachments.GetValueOrDefault(person.Id) ?? [];
      var spouses = spouseFamilies.GetValueOrDefault(person.Id)?.ToArray() ?? [];
      var children = childFamilies.GetValueOrDefault(person.Id)?.ToArray() ?? [];
      individuals.Add(new Individual(person, info, biography, residue, mainPhoto, morePhotos, personAttachments, spouses, children));
    }
    return [.. individuals];
  }

  private static void LinkFamily(Dictionary<int, List<string>> map, int? personId, string xref)
  {
    if (personId is null)
      return;

    if (!map.TryGetValue(personId.Value, out var list))
    {
      map[personId.Value] = list = [];
    }
    list.Add(xref);
  }

  private static void LinkChild(Dictionary<int, List<(string, bool)>> map, int childId, string xref, bool adopted)
  {
    if (!map.TryGetValue(childId, out var list))
    {
      map[childId] = list = [];
    }
    list.Add((xref, adopted));
  }

  private static async Task<GedcomNode> BuildIndividualAsync(Individual individual, CancellationToken token)
  {
    var person = individual.Person;
    var node = new GedcomNode { Tag = GedcomTags.Individual, Xref = $"@I{person.Id}@" };

    // The residue splits into the unmodeled sub-tags of owned tags (merged back into the regenerated NAME /
    // SEX / BIRT / DEAT / NOTE below) and the fully-unmodeled INDI children (re-attached whole at the end).
    var residueRoots = await ParseResidueAsync(individual.Residue, token);
    var (ownedResidual, otherResidue) = PartitionResidue(residueRoots);

    var nameResidual = Residual(ownedResidual, GedcomTags.Name);
    var nameNode = BuildName(individual.Info, person.BiologicalSex, nameResidual);
    if (nameNode is not null)
    {
      node.Add(nameNode);
    }

    var sexResidual = Residual(ownedResidual, GedcomTags.Sex);
    var sexNode = new GedcomNode { Tag = GedcomTags.Sex, Value = GedcomMapping.SexLetter(person.BiologicalSex) };
    sexNode.Add([.. sexResidual]);
    node.Add(sexNode);

    AddEvent(node, GedcomTags.Birth, person.BirthDate, Residual(ownedResidual, GedcomTags.Birth));
    AddEvent(node, GedcomTags.Death, person.DeathDate, Residual(ownedResidual, GedcomTags.Death));

    var noteResidual = Residual(ownedResidual, GedcomTags.Note);
    AddNote(node, individual.Biography, noteResidual);

    await AddPhotoAsync(node, individual.MainPhoto, primary: true, token);
    foreach (var photo in individual.AdditionalPhotos)
    {
      await AddPhotoAsync(node, photo, primary: false, token);
    }
    foreach (var attachment in individual.Attachments)
    {
      await AddAttachmentAsync(node, attachment, token);
    }

    var spouseNodes = individual
      .SpouseFamilies
      .Select(xref => new GedcomNode { Tag = GedcomTags.FamilySpouse, Value = xref });

    var childNodes = individual.ChildFamilies.Select(link =>
    {
      var familyChild = new GedcomNode { Tag = GedcomTags.FamilyChild, Value = link.Xref };
      if (link.Adopted)
      {
        familyChild.Add(new GedcomNode { Tag = GedcomTags.Pedigree, Value = GedcomTags.AdoptedPedigree });
      }
      return familyChild;
    });

    // Re-attach the fully-unmodeled INDI sub-tags stashed at import: the recursive writer re-levels them
    // back under this regenerated INDI, so they emit exactly as they came in.
    node.Add([.. spouseNodes, .. childNodes, .. otherResidue]);

    return node;
  }

  private static async Task<GedcomNode[]> ParseResidueAsync(Data? residue, CancellationToken token)
  {
    if (residue is null)
      return [];

    var text = Encoding.UTF8.GetString(residue.Content);
    return await GedcomReader.ReadAsync(new StringReader(text), token);
  }

  /// <summary>
  /// Splits the residue forest into the residual children of owned tags (keyed by that tag, to be merged
  /// back into the regenerated node) and the fully-unmodeled roots (re-attached to the INDI verbatim).
  /// </summary>
  private static (Dictionary<string, List<GedcomNode>> Owned, List<GedcomNode> Other) PartitionResidue(GedcomNode[] roots)
  {
    var owned = new Dictionary<string, List<GedcomNode>>();
    var other = new List<GedcomNode>();
    foreach (var root in roots)
    {
      // A genuine residual (value-less, only unmodeled children) merges back into the regenerated owned node;
      // a root still carrying a value or a modeled child is a repeated owned tag, re-emitted standalone.
      if (!GedcomMapping.OwnedTagModeledChildren.TryGetValue(root.Tag, out var modeled) || !IsGenuineResidual(root, modeled))
      {
        other.Add(root);
        continue;
      }

      if (!owned.TryGetValue(root.Tag, out var list))
      {
        owned[root.Tag] = list = [];
      }
      list.AddRange(root.Children);
    }
    return (owned, other);
  }

  private static bool IsGenuineResidual(GedcomNode root, HashSet<string> modeled) =>
    root.Value is null && root.Children.All(child => !modeled.Contains(child.Tag));

  private static IReadOnlyList<GedcomNode> Residual(Dictionary<string, List<GedcomNode>> owned, string tag) =>
    owned.TryGetValue(tag, out var list) ? list : [];

  /// <summary>
  /// Emits the biography NOTE with any preserved unmodeled NOTE sub-tags merged under it. Emits nothing when
  /// the person has neither a biography nor preserved sub-tags.
  /// </summary>
  private static void AddNote(GedcomNode individual, Data? biography, IReadOnlyList<GedcomNode> residual)
  {
    if (biography is null && residual.Count == 0)
      return;

    var note = new GedcomNode { Tag = GedcomTags.Note };
    if (biography is not null)
    {
      note.Value = Encoding.UTF8.GetString(biography.Content);
    }
    note.Add([.. residual]);
    individual.Add(note);
  }

  /// <summary>
  /// Emits a GT4 photo as an embedded multimedia object: an <c>OBJE</c> carrying the format (from the
  /// MIME type), a <c>_PRIM Y</c> marker on the main photo, and the image bytes base64-encoded into a
  /// <c>BLOB</c> the writer auto-chunks across CONC lines. This is the form <see cref="GedcomImporter"/>
  /// reads back, so photos round-trip self-contained inside the single .ged file. A tagged photo's
  /// residual children (chiefly <c>TITL</c>) are decoded and merged back onto the OBJE -- a per-node
  /// merge-back, not the shared <see cref="PartitionResidue"/> bucket-by-tag mechanism, since several
  /// independent photos each need their own distinct residual tags, not one pooled per-tag-name bucket.
  /// </summary>
  private static async Task AddPhotoAsync(GedcomNode individual, Data? photo, bool primary, CancellationToken token)
  {
    if (photo is null)
      return;

    var imageBytes = photo.Content;
    GedcomNode? residual = null;
    if (photo.Category.IsTaggedPhoto())
    {
      (imageBytes, residual) = await GedcomPhotoResidue.DecodeAsync(photo.Content, token);
    }

    var obje = new GedcomNode { Tag = GedcomTags.Object };
    var form = GedcomMedia.ToForm(photo.MimeType);
    if (form is not null)
    {
      obje.Add(new GedcomNode { Tag = GedcomTags.Form, Value = form });
    }
    if (primary)
    {
      obje.Add(new GedcomNode { Tag = GedcomTags.Primary, Value = GedcomTags.PrimaryYes });
    }
    var base64 = Convert.ToBase64String(imageBytes);
    obje.Add(new GedcomNode { Tag = GedcomTags.Blob, Value = base64 });
    if (residual is not null)
    {
      obje.Add([.. residual.Children]);
    }
    individual.Add(obje);
  }

  /// <summary>
  /// Emits a GT4 attachment as an embedded multimedia object, mirroring <see cref="AddPhotoAsync"/> minus
  /// the <c>_PRIM</c> marker (attachments have no main/additional concept) and plus a <c>_ATTACH Y</c>
  /// marker, so an image-typed attachment (indistinguishable from a photo by FORM alone) still imports
  /// back as an attachment. An attachment's <c>Content</c> is always a <see cref="GedcomPhotoResidue"/>
  /// envelope, so it is always decoded and its residual (chiefly <c>FILE</c>, the original filename)
  /// merged back onto the regenerated OBJE.
  /// </summary>
  private static async Task AddAttachmentAsync(GedcomNode individual, Data attachment, CancellationToken token)
  {
    var (bytes, residual) = await GedcomPhotoResidue.DecodeAsync(attachment.Content, token);

    var obje = new GedcomNode { Tag = GedcomTags.Object };
    var form = GedcomMedia.ToForm(attachment.MimeType);
    if (form is not null)
    {
      obje.Add(new GedcomNode { Tag = GedcomTags.Form, Value = form });
    }
    obje.Add(new GedcomNode { Tag = GedcomTags.Attachment, Value = GedcomTags.PrimaryYes });
    var base64 = Convert.ToBase64String(bytes);
    obje.Add(new GedcomNode { Tag = GedcomTags.Blob, Value = base64 });
    obje.Add([.. residual.Children]);
    individual.Add(obje);
  }

  /// <summary>
  /// A birth is only emitted when something is known; a death is emitted whenever a death date exists at
  /// all, even with unknown precision, so the fact of death survives the round-trip.
  /// </summary>
  private static void AddEvent(GedcomNode individual, string eventTag, Date? date, IReadOnlyList<GedcomNode> residual)
  {
    // No date and no preserved sub-tags means the event was never recorded: emit nothing.
    if (date is null && residual.Count == 0)
      return;

    var value = date.HasValue ? GedcomDate.ToGedcom(date.Value) : null;
    // Only a birth with no usable date and no sub-tags is dropped here; a death still falls through.
    if (value is null && residual.Count == 0 && eventTag == GedcomTags.Birth)
      return;

    var eventNode = new GedcomNode { Tag = eventTag };
    if (value is not null)
    {
      eventNode.Add(new GedcomNode { Tag = GedcomTags.Date, Value = value });
    }
    eventNode.Add([.. residual]);
    individual.Add(eventNode);
  }

  private static GedcomNode? BuildName(PersonInfo? info, BiologicalSex sex, IReadOnlyList<GedcomNode> residual)
  {
    if (info is null)
      return null;

    var given = SelectName(info, NameType.FirstName, sex);
    var patronymics = SelectNames(info, NameType.Patronymic, sex);
    var surname = SelectName(info, NameType.LastName, sex);

    if (given is null && surname is null)
      return null;

    var givenParts = new List<string>();
    if (given is not null)
    {
      givenParts.Add(given);
    }
    givenParts.AddRange(patronymics);
    var givenFull = string.Join(' ', givenParts);
    var node = new GedcomNode { Tag = GedcomTags.Name, Value = FormatName(givenFull, surname) };
    if (!string.IsNullOrEmpty(givenFull))
    {
      node.Add(new GedcomNode { Tag = GedcomTags.Given, Value = givenFull });
    }
    if (surname is not null)
    {
      node.Add(new GedcomNode { Tag = GedcomTags.Surname, Value = surname });
    }
    // Merge the preserved unmodeled NAME sub-tags (NICK, SPFX, ...) after GIVN/SURN.
    node.Add([.. residual]);
    return node;
  }

  private static string FormatName(string given, string? surname)
  {
    if (string.IsNullOrEmpty(surname))
      return given;

    return string.IsNullOrEmpty(given) ? $"/{surname}/" : $"{given} /{surname}/";
  }

  /// <summary>
  /// Picks a name part by its base type (filtering on the flag bit, not the declension mask), preferring
  /// the spelling that matches the person's sex declension and falling back to any spelling — which is
  /// what lets sex-less people, who carry undeclined names, still export their names.
  /// </summary>
  private static string? SelectName(PersonInfo info, NameType baseType, BiologicalSex sex)
  {
    var candidates = info.Names.Where(n => (n.Type & baseType) != 0).ToList();
    if (candidates.Count == 0)
      return null;

    var wantedDeclension = GedcomMapping.Declension(sex);
    var preferred = candidates.FirstOrDefault(n => wantedDeclension == 0 || (n.Type & wantedDeclension) != 0);
    var chosen = preferred ?? candidates[0];
    return chosen.Value;
  }

  /// <summary>
  /// Like <see cref="SelectName"/> but keeps every part of the base type in stored order, used for
  /// patronymics where a multi-token given name ("Patrick Branwell Josef") becomes several patronymic
  /// records that must all rejoin into the GIVN. Prefers the matching declension, falling back to all.
  /// </summary>
  private static IEnumerable<string> SelectNames(PersonInfo info, NameType baseType, BiologicalSex sex)
  {
    var candidates = info.Names.Where(n => (n.Type & baseType) != 0).ToList();
    var wantedDeclension = GedcomMapping.Declension(sex);
    var matching = candidates.Where(n => wantedDeclension == 0 || (n.Type & wantedDeclension) != 0).ToList();
    var chosen = matching.Count > 0 ? matching : candidates;
    return chosen.Select(n => n.Value);
  }

  private static void WriteFamilies(TextWriter writer, Family[] families)
  {
    foreach (var family in families)
    {
      var node = new GedcomNode { Tag = GedcomTags.Family, Xref = family.Xref };
      if (family.HusbandId is not null)
      {
        node.Add(new GedcomNode { Tag = GedcomTags.Husband, Value = $"@I{family.HusbandId}@" });
      }
      if (family.WifeId is not null)
      {
        node.Add(new GedcomNode { Tag = GedcomTags.Wife, Value = $"@I{family.WifeId}@" });
      }
      foreach (var date in family.MarriageDates)
      {
        AddMarriage(node, date);
      }
      foreach (var (childId, _) in family.Children)
      {
        node.Add(new GedcomNode { Tag = GedcomTags.Child, Value = $"@I{childId}@" });
      }
      GedcomWriter.Write(writer, node);
    }
  }

  private static void AddMarriage(GedcomNode family, Date? date)
  {
    var marriage = new GedcomNode { Tag = GedcomTags.Marriage };
    var value = date.HasValue ? GedcomDate.ToGedcom(date.Value) : null;
    if (value is not null)
    {
      marriage.Add(new GedcomNode { Tag = GedcomTags.Date, Value = value });
    }
    family.Add(marriage);
  }
}
