using System.Diagnostics;

namespace GT4.Core.Project.Dto;

/// <summary>
/// A connection between two nodes of the ancestor-descendant graph. For a
/// <see cref="FamilyTreeRelation.ParentChild"/> edge <see cref="FromId"/> is always the parent and
/// <see cref="ToId"/> the child; for a <see cref="FamilyTreeRelation.Spouse"/> edge the two ids are
/// ordered ascending so the same couple yields a single, de-duplicated edge.
/// </summary>
[DebuggerDisplay("{FromId} -{Relation}-> {ToId}")]
public record class FamilyTreeEdge(int FromId, int ToId, FamilyTreeRelation Relation)
{
  public static FamilyTreeEdge ParentChild(int parentId, int childId) =>
    new(parentId, childId, FamilyTreeRelation.ParentChild);

  public static FamilyTreeEdge Spouse(int leftId, int rightId) =>
    new(Math.Min(leftId, rightId), Math.Max(leftId, rightId), FamilyTreeRelation.Spouse);
}
