using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Text;

namespace GT4.Core.Gedcom;

internal sealed class GedcomImporter : IGedcomImporter
{
  public async Task ImportAsync(IProjectDocument document, TextReader reader, CancellationToken token)
  {
    var records = GedcomReader.Read(reader);
    var notesByXref = records
      .Where(r => r.Tag == GedcomTags.Note && r.Xref is not null)
      .ToDictionary(r => r.Xref!, r => r.Value);

    var individuals = records.Where(r => r.Tag == GedcomTags.Individual).ToList();
    var families = records.Where(r => r.Tag == GedcomTags.Family).ToList();

    // A child's tie to a family is adoptive when its FAMC link carries "PEDI adopted"; that lives on the
    // individual, so it is collected up front and consulted while wiring each family's children.
    var adoptedLinks = CollectAdoptedLinks(individuals);

    // One outer transaction on a single flow: every inner Add* collapses to a SAVEPOINT and the lone
    // root commit stamps the revision, so the import lands all-or-nothing. The two passes stay strictly
    // sequential because a flow-affine transaction cannot be shared across parallel branches.
    using var transaction = await document.BeginTransactionAsync(token);

    var nameCache = new Dictionary<(string Value, NameType Type), Name>();
    var personByXref = new Dictionary<string, Person>();

    foreach (var individual in individuals)
    {
      var person = await ImportIndividualAsync(document, individual, notesByXref, nameCache, token);
      if (individual.Xref is not null)
      {
        personByXref[individual.Xref] = person;
      }
    }

    foreach (var family in families)
    {
      await ImportFamilyAsync(document, family, personByXref, adoptedLinks, token);
    }

    await transaction.CommitAsync(token);
  }

  private static HashSet<(string Child, string Family)> CollectAdoptedLinks(IEnumerable<GedcomNode> individuals)
  {
    var links = new HashSet<(string, string)>();
    foreach (var individual in individuals)
    {
      if (individual.Xref is null)
        continue;

      foreach (var familyChild in individual.ChildrenWithTag(GedcomTags.FamilyChild))
      {
        var pedigree = familyChild.ChildValue(GedcomTags.Pedigree);
        var isAdopted = string.Equals(pedigree, GedcomTags.AdoptedPedigree, StringComparison.OrdinalIgnoreCase);
        if (familyChild.Value is not null && isAdopted)
        {
          links.Add((individual.Xref, familyChild.Value));
        }
      }
    }
    return links;
  }

  private static async Task<Person> ImportIndividualAsync(
    IProjectDocument document,
    GedcomNode individual,
    IReadOnlyDictionary<string, string?> notesByXref,
    Dictionary<(string, NameType), Name> nameCache,
    CancellationToken token)
  {
    var sex = GedcomMapping.ParseSex(individual.ChildValue(GedcomTags.Sex));
    var names = await BuildNamesAsync(document, individual, sex, nameCache, token);
    var biography = BuildBiography(individual, notesByXref);

    var toAdd = PersonFullInfo.Empty with
    {
      BirthDate = ParseEventDate(individual, GedcomTags.Birth) ?? new Date { Status = DateStatus.Unknown },
      DeathDate = ParseEventDate(individual, GedcomTags.Death),
      BiologicalSex = sex,
      Names = names,
      Biography = biography,
    };

    var added = await document.PersonManager.AddPersonAsync(toAdd, token);
    return new Person(added.Id, added.BirthDate, added.DeathDate, added.BiologicalSex);
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
    Dictionary<(string, NameType), Name> nameCache,
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
      var firstName = await GetOrAddNameAsync(document, given.Trim(), NameType.FirstName | declension, nameCache, token);
      names.Add(firstName);
    }
    if (!string.IsNullOrWhiteSpace(surname))
    {
      var lastName = await GetOrAddNameAsync(document, surname.Trim(), NameType.LastName | declension, nameCache, token);
      names.Add(lastName);
    }
    return names.ToArray();
  }

  private static async Task<Name> GetOrAddNameAsync(
    IProjectDocument document,
    string value,
    NameType type,
    Dictionary<(string, NameType), Name> nameCache,
    CancellationToken token)
  {
    var key = (value, type);
    if (nameCache.TryGetValue(key, out var cached))
      return cached;

    var name = await document.Names.AddNameAsync(value, type, null, token);
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
    return new Data(TableBase.NonCommittedId, content, "text/plain", DataCategory.PersonBio);
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
        .ToArray();
      await document.Relatives.AddRelativesAsync(parent, childRelatives, token);
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
