using GT4.Core.Project.Dto;
using GT4.UI.Utils;

namespace GT4.UI.Components.Genealogy;

/// <summary>
/// Strokes the family-tree connectors onto a <see cref="GraphicsView"/>: parent-child links as
/// orthogonal lines with softly rounded right-angle bends, spouse links as straight horizontal lines.
/// </summary>
public sealed class FamilyTreeConnectorsDrawable : IDrawable
{
  public IReadOnlyList<FamilyTreeConnector> Connectors { get; set; } = [];
  public double CornerRadius { get; set; } = 14;
  public Color ParentChildColor { get; set; } = Colors.Gray;
  public Color SpouseColor { get; set; } = Colors.IndianRed;
  public float LineWidth { get; set; } = 2f;

  public void Draw(ICanvas canvas, RectF dirtyRect)
  {
    canvas.StrokeLineJoin = LineJoin.Round;
    canvas.StrokeLineCap = LineCap.Round;
    canvas.StrokeSize = LineWidth;

    foreach (var connector in Connectors)
    {
      canvas.StrokeColor = connector.Relation == FamilyTreeRelation.Spouse ? SpouseColor : ParentChildColor;
      canvas.DrawPath(BuildPath(connector.Points, (float)CornerRadius));
    }
  }

  private static PathF BuildPath(PointF[] points, float radius)
  {
    var path = new PathF();
    if (points.Length == 0)
    {
      return path;
    }

    path.MoveTo(points[0]);

    if (points.Length == 1)
    {
      return path;
    }

    // Round every interior vertex: come up short of the corner, then sweep through it with a quadratic
    // whose control point is the corner itself.
    for (var i = 1; i < points.Length - 1; i++)
    {
      var previous = points[i - 1];
      var corner = points[i];
      var next = points[i + 1];

      var enter = Shorten(corner, previous, radius);
      var exit = Shorten(corner, next, radius);

      path.LineTo(enter);
      path.QuadTo(corner, exit);
    }

    path.LineTo(points[^1]);
    return path;
  }

  // A point on the segment from <paramref name="corner"/> toward <paramref name="target"/>, capped so
  // the rounded notch never eats more than half of a short segment.
  private static PointF Shorten(PointF corner, PointF target, float radius)
  {
    var dx = target.X - corner.X;
    var dy = target.Y - corner.Y;
    var length = MathF.Sqrt((dx * dx) + (dy * dy));
    if (length <= float.Epsilon)
    {
      return corner;
    }

    var distance = MathF.Min(radius, length / 2f);
    var ratio = distance / length;
    return new PointF(corner.X + (dx * ratio), corner.Y + (dy * ratio));
  }
}
