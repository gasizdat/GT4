using GT4.Core.Project.Dto;
using GT4.UI.Components.Genealogy;
using GT4.UI.Utils;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers FamilyTreeConnectorShape's geometry building directly: the stroke-inflated bounding box a
/// connector's Path is placed at (tight around its points, not the whole canvas -- see the class's own
/// remarks on why: staying under the GPU max-texture size), and BuildGeometry's corner-rounding, which
/// shortens a line before an interior vertex and sweeps through it with a quadratic bezier rather than
/// a hard corner.
/// </summary>
public class FamilyTreeConnectorShapeTests
{
  private static FamilyTreeConnector MakeConnector(params PointF[] points) =>
    new(FamilyTreeRelation.ParentChild, points);

  [Fact]
  public void Create_sets_the_paths_fixed_rendering_properties()
  {
    var path = FamilyTreeConnectorShape.Create(MakeConnector(new PointF(0, 0), new PointF(10, 10)), cornerRadius: 0, lineWidth: 1, Colors.Black);

    Assert.Equal(PenLineJoin.Round, path.StrokeLineJoin);
    Assert.Equal(PenLineCap.Round, path.StrokeLineCap);
    Assert.Equal(Stretch.None, path.Aspect);
    Assert.True(path.InputTransparent);
    Assert.Equal(AbsoluteLayoutFlags.None, AbsoluteLayout.GetLayoutFlags(path));
  }

  [Fact]
  public void Update_sets_the_stroke_from_color_and_lineWidth()
  {
    var path = FamilyTreeConnectorShape.Create(MakeConnector(new PointF(0, 0), new PointF(10, 10)), cornerRadius: 0, lineWidth: 4, Colors.Red);

    Assert.Equal(Colors.Red, path.Stroke);
    Assert.Equal(4, path.StrokeThickness);
  }

  [Fact]
  public void The_layout_bounds_are_the_points_bounding_box_inflated_by_half_the_stroke_width()
  {
    var path = FamilyTreeConnectorShape.Create(
      MakeConnector(new PointF(10, 10), new PointF(50, 50)), cornerRadius: 0, lineWidth: 2, Colors.Black);

    var bounds = AbsoluteLayout.GetLayoutBounds(path);

    Assert.Equal(new Rect(9, 9, 42, 42), bounds);
  }

  [Fact]
  public void A_two_point_connector_builds_a_single_straight_segment_in_local_space()
  {
    var path = FamilyTreeConnectorShape.Create(
      MakeConnector(new PointF(10, 10), new PointF(50, 50)), cornerRadius: 0, lineWidth: 2, Colors.Black);

    var geometry = Assert.IsType<PathGeometry>(path.Data);
    var figure = Assert.Single(geometry.Figures);
    // Bounds origin is (9, 9) (see the inflate test above), so points shift into local space by (-9, -9).
    Assert.Equal(new Point(1, 1), figure.StartPoint);
    var segment = Assert.IsType<LineSegment>(Assert.Single(figure.Segments));
    Assert.Equal(new Point(41, 41), segment.Point);
  }

  [Fact]
  public void An_interior_vertex_is_shortened_and_swept_through_with_a_quadratic_bezier()
  {
    // lineWidth 0 keeps local space identical to the connector's own points (no bounds inflation).
    var path = FamilyTreeConnectorShape.Create(
      MakeConnector(new PointF(0, 0), new PointF(10, 0), new PointF(10, 10)),
      cornerRadius: 2,
      lineWidth: 0,
      Colors.Black);

    var geometry = Assert.IsType<PathGeometry>(path.Data);
    var figure = Assert.Single(geometry.Figures);
    Assert.Equal(new Point(0, 0), figure.StartPoint);
    Assert.Equal(3, figure.Segments.Count);

    var enter = Assert.IsType<LineSegment>(figure.Segments[0]);
    Assert.Equal(new Point(8, 0), enter.Point);

    var corner = Assert.IsType<QuadraticBezierSegment>(figure.Segments[1]);
    Assert.Equal(new Point(10, 0), corner.Point1);
    Assert.Equal(new Point(10, 2), corner.Point2);

    var exit = Assert.IsType<LineSegment>(figure.Segments[2]);
    Assert.Equal(new Point(10, 10), exit.Point);
  }

  [Fact]
  public void Update_re_specifies_an_existing_path_instance_for_a_new_connector()
  {
    var path = FamilyTreeConnectorShape.Create(
      MakeConnector(new PointF(0, 0), new PointF(10, 10)), cornerRadius: 0, lineWidth: 1, Colors.Black);

    FamilyTreeConnectorShape.Update(
      path, MakeConnector(new PointF(0, 0), new PointF(20, 20)), cornerRadius: 0, lineWidth: 1, Colors.Blue);

    Assert.Equal(Colors.Blue, path.Stroke);
    Assert.Equal(new Rect(-0.5, -0.5, 21, 21), AbsoluteLayout.GetLayoutBounds(path));
  }
}
