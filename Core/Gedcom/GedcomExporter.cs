using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
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

  public async Task ExportAsync(IProjectDocument document, TextWriter writer, CancellationToken token)
  {
    var persons = await document.Persons.GetPersonsAsync(token);
    var personById = persons.ToDictionary(p => p.Id);
    var personInfos = await document.PersonManager.GetPersonInfosAsync(persons, selectMainPhoto: false, token);
    var infoById = personInfos.ToDictionary(p => p.Id);
    var biographies = await document.PersonData.GetPersonDataSetAsync(persons, DataCategory.PersonBio, token);
    var relatives = await document.Relatives.GetRelativesForPersonsAsync(persons, token);

    var families = BuildFamilies(relatives, personById);

    WriteHeader(writer);
    WriteIndividuals(writer, persons, infoById, biographies, families);
    WriteFamilies(writer, families);
    GedcomWriter.Write(writer, new GedcomNode { Tag = GedcomTags.Trailer });
  }

  private static List<Family> BuildFamilies(Dictionary<int, Relative[]> relatives, Dictionary<int, Person> personById)
  {
    var edges = CollectEdges(relatives);

    var byKey = new Dictionary<(int? Husband, int? Wife), Family>();

    Family GetFamily(IReadOnlyList<int> parentIds)
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
    return ordered;
  }

  private static void AddChildren(
    HashSet<(int Child, int Parent)> edges,
    bool adopted,
    Func<IReadOnlyList<int>, Family> getFamily)
  {
    var childrenByParents = edges
      .GroupBy(e => e.Child)
      .ToDictionary(g => g.Key, g => g.Select(e => e.Parent).Distinct().ToList());

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
  private static (int? Husband, int? Wife) OrderParents(IReadOnlyList<int> parentIds, Dictionary<int, Person> personById)
  {
    if (parentIds.Count == 1)
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

  private static void WriteIndividuals(
    TextWriter writer,
    Person[] persons,
    Dictionary<int, PersonInfo> infoById,
    Dictionary<int, Data[]> biographies,
    List<Family> families)
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

    foreach (var person in persons.OrderBy(p => p.Id))
    {
      var info = infoById.GetValueOrDefault(person.Id);
      var biography = biographies.GetValueOrDefault(person.Id)?.FirstOrDefault();
      var node = BuildIndividual(person, info, biography, spouseFamilies, childFamilies);
      GedcomWriter.Write(writer, node);
    }
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

  private static GedcomNode BuildIndividual(
    Person person,
    PersonInfo? info,
    Data? biography,
    Dictionary<int, List<string>> spouseFamilies,
    Dictionary<int, List<(string Xref, bool Adopted)>> childFamilies)
  {
    var node = new GedcomNode { Tag = GedcomTags.Individual, Xref = $"@I{person.Id}@" };

    var nameNode = BuildName(info, person.BiologicalSex);
    if (nameNode is not null)
    {
      node.Add(nameNode);
    }

    node.Add(new GedcomNode { Tag = GedcomTags.Sex, Value = GedcomMapping.SexLetter(person.BiologicalSex) });
    AddEvent(node, GedcomTags.Birth, person.BirthDate);
    AddEvent(node, GedcomTags.Death, person.DeathDate);

    if (biography is not null)
    {
      var text = Encoding.UTF8.GetString(biography.Content);
      node.Add(new GedcomNode { Tag = GedcomTags.Note, Value = text });
    }

    foreach (var xref in spouseFamilies.GetValueOrDefault(person.Id) ?? [])
    {
      node.Add(new GedcomNode { Tag = GedcomTags.FamilySpouse, Value = xref });
    }
    foreach (var (xref, adopted) in childFamilies.GetValueOrDefault(person.Id) ?? [])
    {
      var familyChild = new GedcomNode { Tag = GedcomTags.FamilyChild, Value = xref };
      if (adopted)
      {
        familyChild.Add(new GedcomNode { Tag = GedcomTags.Pedigree, Value = GedcomTags.AdoptedPedigree });
      }
      node.Add(familyChild);
    }

    return node;
  }

  /// <summary>
  /// A birth date is only emitted when something is known; a death is emitted whenever a death date
  /// exists at all (even with unknown precision), so the fact of death survives the round-trip.
  /// </summary>
  private static void AddEvent(GedcomNode individual, string eventTag, Date? date)
  {
    if (date is null && eventTag == GedcomTags.Birth)
      return;

    var value = date.HasValue ? GedcomDate.ToGedcom(date.Value) : null;
    if (value is null && eventTag == GedcomTags.Birth)
      return;

    var eventNode = new GedcomNode { Tag = eventTag };
    if (value is not null)
    {
      eventNode.Add(new GedcomNode { Tag = GedcomTags.Date, Value = value });
    }
    individual.Add(eventNode);
  }

  private static GedcomNode? BuildName(PersonInfo? info, BiologicalSex sex)
  {
    if (info is null)
      return null;

    var given = SelectName(info, NameType.FirstName, sex);
    var patronymic = SelectName(info, NameType.Patronymic, sex);
    var surname = SelectName(info, NameType.LastName, sex);

    if (given is null && surname is null)
      return null;

    var givenFull = string.Join(' ', new[] { given, patronymic }.Where(p => p is not null));
    var node = new GedcomNode { Tag = GedcomTags.Name, Value = FormatName(givenFull, surname) };
    if (!string.IsNullOrEmpty(givenFull))
    {
      node.Add(new GedcomNode { Tag = GedcomTags.Given, Value = givenFull });
    }
    if (surname is not null)
    {
      node.Add(new GedcomNode { Tag = GedcomTags.Surname, Value = surname });
    }
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

  private static void WriteFamilies(TextWriter writer, List<Family> families)
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
