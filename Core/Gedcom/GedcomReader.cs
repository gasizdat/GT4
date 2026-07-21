using System.Text;

namespace GT4.Core.Gedcom;

/// <summary>
/// Parses the line-oriented GEDCOM grammar (<c>level [@xref@] tag [value]</c>) into a forest of
/// top-level <see cref="GedcomNode"/> records. CONC/CONT continuation lines are folded back into the
/// value of the line they extend, so callers see whole logical values.
/// </summary>
internal static class GedcomReader
{
  public static async Task<GedcomNode[]> ReadAsync(TextReader reader, CancellationToken token)
  {
    var roots = new List<GedcomNode>();
    // The most recent node seen at each level; a node attaches under the node one level shallower.
    var openNodes = new Dictionary<int, GedcomNode>();
    // Continuations accumulate here instead of via repeated string concatenation, which is quadratic
    // for records with many CONC/CONT lines; flushed into GedcomNode.Value once reading completes.
    var continuations = new Dictionary<GedcomNode, StringBuilder>();

    string? line;
    while ((line = await reader.ReadLineAsync(token)) != null)
    {
      var parsed = ParseLine(line);
      if (parsed is null)
        continue;

      var (level, xref, tag, value) = parsed.Value;

      if (tag is GedcomTags.Concatenation or GedcomTags.Continuation)
      {
        AppendContinuation(openNodes, continuations, level, tag, value);
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

    foreach (var (node, builder) in continuations)
      node.Value = builder.ToString();

    return [.. roots];
  }

  private static void AppendContinuation(Dictionary<int, GedcomNode> openNodes, Dictionary<GedcomNode, StringBuilder> continuations, int level, string tag, string? value)
  {
    if (!openNodes.TryGetValue(level - 1, out var owner))
      return;

    if (!continuations.TryGetValue(owner, out var builder))
    {
      builder = new StringBuilder(owner.Value ?? string.Empty);
      continuations[owner] = builder;
    }

    var separator = tag == GedcomTags.Continuation ? "\n" : string.Empty;
    builder.Append(separator).Append(value);
  }

  private static (int Level, string? Xref, string Tag, string? Value)? ParseLine(string rawLine)
  {
    // Only leading whitespace is stripped: a trailing space can be a significant, round-tripped part of the value.
    var line = rawLine.TrimStart();
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
  /// Consumes a leading <c>@xref@</c> record identifier from <paramref name="rest"/>, if present. A
  /// pointer used as a value (e.g. <c>HUSB @I1@</c>) is not leading, so it stays in the value untouched.
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
