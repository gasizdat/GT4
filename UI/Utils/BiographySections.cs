namespace GT4.UI.Utils;

public static class BiographySections
{
  // The biography block doubles as the home for the read-only GEDCOM details: the stored bio first, then
  // the person's own residual tags, then their couples', so a person carrying only imported GEDCOM data
  // still shows the block.
  public static string Combine(params string?[] sections)
  {
    var present = sections.Where(section => !string.IsNullOrWhiteSpace(section));
    return string.Join("\n\n", present);
  }
}
