using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.Core.Gedcom.Tests;

/// <summary>
/// Reduces a document to a name-keyed edge graph so two documents with different surrogate ids can be
/// compared, and looks persons up by display name. Spouse edges are keyed by the unordered name pair plus
/// marriage date; parent links by the "child &lt;- parent" name pair (adoptive links carry an "adopt:"
/// prefix). GT4 stores these as pairwise edges, so this is the shape both export round-trips and external
/// sample imports are asserted against.
/// </summary>
internal static class GedcomTestGraph
{
  private static string DateKey(Date? date) =>
    date is null ? "none" : $"{date.Value.Code}:{date.Value.Status}";

  public static async Task<Dictionary<string, Person>> PersonsByNameAsync(ProjectDocument document, CancellationToken token)
  {
    var persons = await document.Persons.GetPersonsAsync(token);
    var infos = await document.PersonManager.GetPersonInfosAsync(persons, selectMainPhoto: false, token);
    var nameById = infos.ToDictionary(p => p.Id, p => p.DisplayName);
    return persons.ToDictionary(p => nameById[p.Id]);
  }

  public static async Task<(HashSet<string> Spouses, HashSet<string> ParentChild)> ExtractAsync(ProjectDocument document, CancellationToken token)
  {
    var persons = await document.Persons.GetPersonsAsync(token);
    var infos = await document.PersonManager.GetPersonInfosAsync(persons, selectMainPhoto: false, token);
    var nameById = infos.ToDictionary(p => p.Id, p => p.DisplayName);
    var relatives = await document.Relatives.GetRelativesForPersonsAsync(persons, token);

    var spouses = new HashSet<string>();
    var parentChild = new HashSet<string>();
    foreach (var (ownerId, ownerRelatives) in relatives)
    {
      foreach (var relative in ownerRelatives)
      {
        switch (relative.Type)
        {
          case RelationshipType.Spouse:
            var pair = new[] { nameById[ownerId], nameById[relative.Id] }.OrderBy(n => n);
            spouses.Add($"{string.Join("+", pair)}@{DateKey(relative.Date)}");
            break;
          case RelationshipType.Parent:
            parentChild.Add($"{nameById[ownerId]}<-{nameById[relative.Id]}");
            break;
          case RelationshipType.Child:
            parentChild.Add($"{nameById[relative.Id]}<-{nameById[ownerId]}");
            break;
          case RelationshipType.AdoptiveParent:
            parentChild.Add($"adopt:{nameById[ownerId]}<-{nameById[relative.Id]}");
            break;
          case RelationshipType.AdoptiveChild:
            parentChild.Add($"adopt:{nameById[relative.Id]}<-{nameById[ownerId]}");
            break;
        }
      }
    }

    return (spouses, parentChild);
  }
}
