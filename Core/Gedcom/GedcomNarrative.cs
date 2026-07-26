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
    return await ParseTextAsync(text, token);
  }

  // A couple's residue is stored as Metadata text rather than in a blob, so it arrives already decoded.
  internal static async Task<GedcomFact[]> ParseTextAsync(string? residue, CancellationToken token)
  {
    if (string.IsNullOrEmpty(residue))
      return [];

    var roots = await GedcomReader.ReadAsync(new StringReader(residue), token);
    return [.. roots.Select(ToFact)];
  }

  private static GedcomFact ToFact(GedcomNode node)
  {
    var children = node.Children.Select(ToFact);
    return new GedcomFact(node.Tag, node.Value, [.. children]);
  }
}
