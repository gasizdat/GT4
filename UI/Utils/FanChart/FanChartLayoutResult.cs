using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

public sealed record FanChartLayoutResult(
  FamilyTreeNode Center,
  FanChartSegment[] Segments,
  double SweepDegrees,
  Size CanvasSize,
  Point Origin,
  FanChartMetrics Metrics);
