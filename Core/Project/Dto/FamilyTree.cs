namespace GT4.Core.Project.Dto;

/// <summary>
/// An ancestor-descendant graph centred on a single person, ready to be laid out as a tree:
/// ancestors fan upward, descendants downward and spouses sit alongside on the same generation.
/// </summary>
public record class FamilyTree(
  int CenterId,
  IReadOnlyList<FamilyTreeNode> Nodes,
  IReadOnlyList<FamilyTreeEdge> Edges)
{
  public static readonly FamilyTree Empty = new(0, [], []);
}
