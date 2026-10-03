using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

/// <summary>
/// An orthogonal poly-line, ready to be stroked with rounded bends. A loop draws a relationship the rows
/// cannot hold, such as a parent that pedigree collapse put on its child's own row.
/// </summary>
public sealed record FamilyTreeConnector(FamilyTreeRelation Relation, PointF[] Points, bool IsLoop = false);
