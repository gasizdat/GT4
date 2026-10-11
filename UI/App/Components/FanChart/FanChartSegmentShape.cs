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
  // In multiples of the stroke thickness.
  private const double RepeatDash = 3;

  public static Path Create(FanChartSegment segment, Point origin, double lineWidth, Color fill, Color stroke)
  {
    var path = new Path
    {
      Fill = fill,
      Stroke = stroke,
      StrokeLineJoin = PenLineJoin.Round,
      StrokeDashArray = segment.IsRepeat ? [RepeatDash, RepeatDash] : [],
      Aspect = Stretch.None,
      InputTransparent = true,
    };
    AbsoluteLayout.SetLayoutFlags(path, AbsoluteLayoutFlags.None);
    Update(path, segment, origin, lineWidth);
    return path;
  }

  // The bounds follow the stroke, so a thicker one isn't clipped.
  public static void Update(Path path, FanChartSegment segment, Point origin, double lineWidth)
  {
    var half = lineWidth / 2;
    var sector = FanChartLayout.SectorBounds(origin, segment.InnerRadius, segment.OuterRadius, segment.StartAngle, segment.SweepAngle);
    var bounds = sector.Inflate(half, half);
    var localOrigin = new Point(origin.X - bounds.Left, origin.Y - bounds.Top);
    path.Data = BuildGeometry(segment, localOrigin);
    path.StrokeThickness = lineWidth;
    AbsoluteLayout.SetLayoutBounds(path, bounds);
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
