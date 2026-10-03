using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

/// <summary>A node together with the rectangle it occupies on the canvas.</summary>
public sealed record FamilyTreeNodeLayout(FamilyTreeNode Node, Rect Bounds);
