using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

/// <summary>
/// An orthogonal poly-line, ready to be stroked with rounded bends. A loop draws a relationship the rows
/// cannot hold, such as a parent that pedigree collapse put on its child's own row.
/// </summary>
/// <param name="PersonIds">
/// Whose lines this is: everyone whose way to a parent, a child or a spouse runs along it. A sibship bar
/// is split where that changes, so a sibling's stub is never part of another child's way up.
/// </param>
public sealed record FamilyTreeConnector(FamilyTreeRelation Relation, PointF[] Points, int[] PersonIds, bool IsLoop = false);
