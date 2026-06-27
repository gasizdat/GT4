using GT4.Core.Project.Dto;
using System.Text;

namespace GT4.Core.Gedcom;

/// <summary>
/// Turns a person's verbatim INDI residue blob (stored as <see cref="DataCategory.PersonGedcomTags"/>) back
/// into a forest of <see cref="GedcomFact"/> for display. It re-uses the GEDCOM reader, so the nesting and
/// order match what was imported; it adds no user-facing text, leaving labels and layout to the UI.
/// </summary>
public static class GedcomNarrative
{
  public static async Task<GedcomFact[]> ParseAsync(Data? residue, CancellationToken token)
  {
    if (residue is null || residue.Content.Length == 0)
      return [];

    var text = Encoding.UTF8.GetString(residue.Content);
    var roots = await GedcomReader.ReadAsync(new StringReader(text), token);
    return [.. roots.Select(ToFact)];
  }

  private static GedcomFact ToFact(GedcomNode node)
  {
    var children = node.Children.Select(ToFact);
    return new GedcomFact(node.Tag, node.Value, [.. children]);
  }
}
