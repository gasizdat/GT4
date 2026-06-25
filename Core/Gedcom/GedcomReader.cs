namespace GT4.Core.Gedcom;

/// <summary>
/// Parses the line-oriented GEDCOM grammar (<c>level [@xref@] tag [value]</c>) into a forest of
/// top-level <see cref="GedcomNode"/> records. CONC/CONT continuation lines are folded back into the
/// value of the line they extend, so callers see whole logical values.
/// </summary>
internal static class GedcomReader
{
  public static List<GedcomNode> Read(TextReader reader)
  {
    var roots = new List<GedcomNode>();
    // The most recent node seen at each level; a node attaches under the node one level shallower.
    var openNodes = new Dictionary<int, GedcomNode>();

    string? line;
    while ((line = reader.ReadLine()) != null)
    {
      var parsed = ParseLine(line);
      if (parsed is null)
        continue;

      var (level, xref, tag, value) = parsed.Value;

      if (tag is GedcomTags.Concatenation or GedcomTags.Continuation)
      {
        AppendContinuation(openNodes, level, tag, value);
        continue;
      }

      var node = new GedcomNode { Tag = tag, Xref = xref, Value = value };
      if (level == 0)
      {
        roots.Add(node);
      }
      else if (openNodes.TryGetValue(level - 1, out var parent))
      {
        parent.Children.Add(node);
      }
      openNodes[level] = node;
    }

    return roots;
  }

  private static void AppendContinuation(Dictionary<int, GedcomNode> openNodes, int level, string tag, string? value)
  {
    if (!openNodes.TryGetValue(level - 1, out var owner))
      return;

    var separator = tag == GedcomTags.Continuation ? "\n" : string.Empty;
    owner.Value = (owner.Value ?? string.Empty) + separator + (value ?? string.Empty);
  }

  private static (int Level, string? Xref, string Tag, string? Value)? ParseLine(string rawLine)
  {
    var line = rawLine.Trim();
    if (line.Length == 0)
      return null;

    var firstSpace = line.IndexOf(' ');
    if (firstSpace < 0)
      return null;

    var levelText = line[..firstSpace];
    if (!int.TryParse(levelText, out var level))
      return null;

    var rest = line[(firstSpace + 1)..];
    var xref = TryTakeXref(ref rest);

    var tagSpace = rest.IndexOf(' ');
    string tag;
    string? value;
    if (tagSpace < 0)
    {
      tag = rest;
      value = null;
    }
    else
    {
      tag = rest[..tagSpace];
      value = rest[(tagSpace + 1)..];
    }

    return tag.Length == 0 ? null : (level, xref, tag, value);
  }

  /// <summary>
  /// A leading <c>@xref@</c> right after the level is the record identifier; consumes it from
  /// <paramref name="rest"/> and returns it. A pointer used as a value (e.g. <c>HUSB @I1@</c>) keeps its
  /// <c>@</c> delimiters and stays in the value, so it is never mistaken for an identifier here.
  /// </summary>
  private static string? TryTakeXref(ref string rest)
  {
    if (!rest.StartsWith('@'))
      return null;

    var end = rest.IndexOf('@', 1);
    if (end <= 0)
      return null;

    var xref = rest[..(end + 1)];
    rest = rest.Length > end + 1 ? rest[(end + 2)..] : string.Empty;
    return xref;
  }
}
