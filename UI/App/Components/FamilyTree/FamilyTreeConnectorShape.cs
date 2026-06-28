using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace GT4.UI.Components.Genealogy;

/// <summary>
/// Builds one family-tree connector as a vector <see cref="Path"/> placed in the node
/// <see cref="AbsoluteLayout"/>. Individual shapes render as composition vector geometry that scrolls
/// in lockstep with the nodes, and — unlike a single canvas-spanning GraphicsView — never allocate a
/// surface larger than the GPU max texture size (16384px), which a tall tree's connectors would hit.
/// </summary>
internal static class FamilyTreeConnectorShape
{
  public static Path Create(FamilyTreeConnector connector, double cornerRadius, double lineWidth, Color color)
  {
    var path = new Path
    {
      StrokeLineJoin = PenLineJoin.Round,
      StrokeLineCap = PenLineCap.Round,
      Aspect = Stretch.None,
      InputTransparent = true,
    };
    AbsoluteLayout.SetLayoutFlags(path, AbsoluteLayoutFlags.None);
    Update(path, connector, cornerRadius, lineWidth, color);
    return path;
  }

  // Re-specifies an existing path for another connector so the family tree can pool and reuse a fixed
  // set of shapes across loads instead of recreating hundreds of them on every incremental load.
  public static void Update(Path path, FamilyTreeConnector connector, double cornerRadius, double lineWidth, Color color)
  {
    var points = connector.Points;
    var half = lineWidth / 2;
    var minX = points.Min(p => p.X);
    var minY = points.Min(p => p.Y);
    var maxX = points.Max(p => p.X);
    var maxY = points.Max(p => p.Y);

    // Tight, stroke-inflated bounds keep each shape small; the geometry is built in this local space so
    // the line sits at the same place it does today regardless of where on the canvas the shape lands.
    var originX = minX - half;
    var originY = minY - half;
    var bounds = new Rect(originX, originY, (maxX - minX) + lineWidth, (maxY - minY) + lineWidth);
    var local = points.Select(p => new Point(p.X - originX, p.Y - originY)).ToArray();

    path.Data = BuildGeometry(local, cornerRadius);
    path.Stroke = color;
    path.StrokeThickness = lineWidth;
    AbsoluteLayout.SetLayoutBounds(path, bounds);
  }

  // Mirrors the former FamilyTreeConnectorsDrawable: round every interior vertex by coming up short of
  // the corner, then sweeping through it with a quadratic whose control point is the corner itself.
  private static Geometry BuildGeometry(Point[] points, double radius)
  {
    var figure = new PathFigure { StartPoint = points[0] };

    for (var i = 1; i < points.Length - 1; i++)
    {
      var previous = points[i - 1];
      var corner = points[i];
      var next = points[i + 1];
      var enter = Shorten(corner, previous, radius);
      var exit = Shorten(corner, next, radius);
      figure.Segments.Add(new LineSegment { Point = enter });
      figure.Segments.Add(new QuadraticBezierSegment { Point1 = corner, Point2 = exit });
    }

    figure.Segments.Add(new LineSegment { Point = points[^1] });
    return new PathGeometry { Figures = { figure } };
  }

  // A point on the segment from <paramref name="corner"/> toward <paramref name="target"/>, capped so
  // the rounded notch never eats more than half of a short segment.
  private static Point Shorten(Point corner, Point target, double radius)
  {
    var dx = target.X - corner.X;
    var dy = target.Y - corner.Y;
    var length = Math.Sqrt((dx * dx) + (dy * dy));
    if (length <= double.Epsilon)
    {
      return corner;
    }

    var distance = Math.Min(radius, length / 2);
    var ratio = distance / length;
    return new Point(corner.X + (dx * ratio), corner.Y + (dy * ratio));
  }
}
