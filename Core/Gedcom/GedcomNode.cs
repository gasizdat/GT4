namespace GT4.Core.Gedcom;

/// <summary>
/// One node in a GEDCOM record tree: a tag with an optional pointer/identifier (<see cref="Xref"/>),
/// an optional <see cref="Value"/> and nested <see cref="Children"/>. <see cref="Value"/> is mutable so
/// the reader can fold CONC/CONT continuation lines back into the line they extend.
/// </summary>
internal sealed class GedcomNode
{
  public required string Tag { get; init; }
  public string? Xref { get; init; }
  public string? Value { get; set; }
  public List<GedcomNode> Children { get; } = [];

  public GedcomNode Add(GedcomNode child)
  {
    Children.Add(child);
    return this;
  }

  public GedcomNode? Child(string tag) => Children.FirstOrDefault(c => c.Tag == tag);

  public IEnumerable<GedcomNode> ChildrenWithTag(string tag) => Children.Where(c => c.Tag == tag);

  public string? ChildValue(string tag) => Child(tag)?.Value;
}
