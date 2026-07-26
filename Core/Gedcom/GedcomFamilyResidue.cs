using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;

namespace GT4.Core.Gedcom;

/// <summary>
/// The couple-level counterpart of <see cref="GedcomNarrative"/>. A FAM record's preserved sub-tags -- the
/// family's own DIV/ENGA/NCHI/CHAN, each marriage's PLAC/SOUR/TYPE, and the repeated MARR events GT4 keeps
/// no edge for -- live in the Metadata table keyed by the couple, so nothing reachable from a person leads
/// to them. Read here from a person's side of the marriage and returned as one FAM fact per couple, valued
/// with the spouse's name, so the person page can show them the way it shows the person's own residue.
/// </summary>
public static class GedcomFamilyResidue
{
  public static async Task<GedcomFact?> ReadAsync(
    IProjectDocument document,
    int personId,
    int spouseId,
    string spouseName,
    IEnumerable<Date?> marriageDates,
    CancellationToken token)
  {
    var familyKey = GedcomMetadata.FamilyKey(personId, spouseId);
    var marriageKeys = marriageDates.Select(date => GedcomMetadata.MarriageKey(familyKey, date));
    var facts = new List<GedcomFact>();

    // Sequential: the reads take turns on the project's single connection, as every other table read does.
    foreach (var key in marriageKeys.Prepend(familyKey))
    {
      var text = await document.Metadata.GetAsync<string>(key, token);
      var parsed = await GedcomNarrative.ParseTextAsync(text, token);
      facts.AddRange(parsed);
    }

    return facts.Count == 0 ? null : new GedcomFact(GedcomTags.Family, spouseName, [.. facts]);
  }
}
