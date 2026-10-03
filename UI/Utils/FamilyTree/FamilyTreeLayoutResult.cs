namespace GT4.UI.Utils.Genealogy;

public sealed record FamilyTreeLayoutResult(
  IReadOnlyList<FamilyTreeNodeLayout> Nodes,
  IReadOnlyList<FamilyTreeConnector> Connectors,
  Size CanvasSize,
  Point CenterTopLeft,
  FamilyTreeLayoutMetrics Metrics);
