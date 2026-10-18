using GT4.UI.Utils.Genealogy;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace GT4.UI.Components.Genealogy;

/// <summary>
/// Builds one fan-chart segment as a vector <see cref="Path"/> in tight bounds of its own, so no shape
/// allocates a surface anywhere near the GPU max texture size however many rings the chart has.
/// </summary>
public static class FanChartSegmentShape
{
  // Absolute, not in stroke thicknesses as a dash array counts, so a thicker outline over a segment
  // dashes in step with it.
  private const double RepeatDashLength = 4.5;

  public static Path Create(FanChartSegment segment, Point origin, double lineWidth, Color fill, Color stroke)
  {
    // The bounds follow the stroke, so a thicker one isn't clipped.
    var half = lineWidth / 2;
    var sector = FanChartLayout.SectorBounds(origin, segment.InnerRadius, segment.OuterRadius, segment.StartAngle, segment.SweepAngle);
    var bounds = sector.Inflate(half, half);
    var localOrigin = new Point(origin.X - bounds.Left, origin.Y - bounds.Top);
    var dash = RepeatDashLength / lineWidth;
    var path = new Path
    {
      Data = BuildGeometry(segment, localOrigin),
      Fill = fill,
      Stroke = stroke,
      StrokeThickness = lineWidth,
      StrokeLineJoin = PenLineJoin.Round,
      StrokeDashArray = segment.IsRepeat ? [dash, dash] : [],
      Aspect = Stretch.None,
      InputTransparent = true,
    };
    AbsoluteLayout.SetLayoutFlags(path, AbsoluteLayoutFlags.None);
    AbsoluteLayout.SetLayoutBounds(path, bounds);
    return path;
  }

  // Bearings grow clockwise on screen, so the outer arc runs clockwise and the inner one back.
  private static Geometry BuildGeometry(FanChartSegment segment, Point origin)
  {
    var endAngle = segment.StartAngle + segment.SweepAngle;
    var isLargeArc = segment.SweepAngle > 180;
    var outerStart = FanChartLayout.PointAt(origin, segment.OuterRadius, segment.StartAngle);
    var outerEnd = FanChartLayout.PointAt(origin, segment.OuterRadius, endAngle);
    var innerEnd = FanChartLayout.PointAt(origin, segment.InnerRadius, endAngle);
    var innerStart = FanChartLayout.PointAt(origin, segment.InnerRadius, segment.StartAngle);
    var outerSize = new Size(segment.OuterRadius, segment.OuterRadius);
    var innerSize = new Size(segment.InnerRadius, segment.InnerRadius);
    var figure = new PathFigure
    {
      StartPoint = outerStart,
      IsClosed = true,
      Segments =
      {
        new ArcSegment(outerEnd, outerSize, 0, SweepDirection.Clockwise, isLargeArc),
        new LineSegment(innerEnd),
        new ArcSegment(innerStart, innerSize, 0, SweepDirection.CounterClockwise, isLargeArc),
      },
    };
    return new PathGeometry { Figures = { figure } };
  }
}
