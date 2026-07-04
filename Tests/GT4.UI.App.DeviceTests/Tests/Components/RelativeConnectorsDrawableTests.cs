using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers RelativeConnectorsDrawable.Draw directly against a mocked ICanvas: the no-op guard for a
/// root row (Depth == 0) or no Row at all, the per-column split between an inherited ancestor trunk
/// (columns left of this row's own, drawn only when that column's trunk still continues) and this
/// row's own trunk (always drawn down to the photo centre, continuing to the bottom only when this
/// row itself has a later sibling), and the horizontal stub every row draws out to the content.
/// </summary>
public class RelativeConnectorsDrawableTests
{
  private static RelativeRow MakeRow(int depth, bool[] ancestorContinues)
  {
    var relative = new RelativeInfo(
      new PersonInfo(1, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male, [], null),
      RelationshipType.Parent,
      null,
      Generation.Parent,
      Consanguinity.Zero);
    var isLast = ancestorContinues.Length == 0 || !ancestorContinues[^1];
    return new RelativeRow(relative, null, depth, isLast, ancestorContinues, new Command(() => { }));
  }

  [Fact]
  public void Draw_does_nothing_when_there_is_no_Row()
  {
    var canvas = new Mock<ICanvas>(MockBehavior.Strict);
    var drawable = new RelativeConnectorsDrawable { Row = null };

    drawable.Draw(canvas.Object, new RectF(0, 0, 100, 100));

    canvas.VerifyNoOtherCalls();
  }

  [Fact]
  public void Draw_does_nothing_for_a_root_row()
  {
    var canvas = new Mock<ICanvas>(MockBehavior.Strict);
    var drawable = new RelativeConnectorsDrawable { Row = MakeRow(0, []) };

    drawable.Draw(canvas.Object, new RectF(0, 0, 100, 100));

    canvas.VerifyNoOtherCalls();
  }

  [Fact]
  public void A_single_column_row_that_is_the_last_sibling_stops_its_trunk_at_the_photo_centre()
  {
    var canvas = new Mock<ICanvas>();
    var drawable = new RelativeConnectorsDrawable
    {
      Row = MakeRow(1, [false]),
      PhotoCenterY = 25,
      Indent = 20,
    };

    drawable.Draw(canvas.Object, new RectF(0, 0, 100, 100));

    canvas.Verify(c => c.DrawLine(10, 0, 10, 25), Times.Once());
    canvas.Verify(c => c.DrawLine(10, 25, 20, 25), Times.Once());
    canvas.Verify(c => c.DrawLine(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Exactly(2));
  }

  [Fact]
  public void A_single_column_row_with_a_later_sibling_continues_its_trunk_to_the_bottom()
  {
    var canvas = new Mock<ICanvas>();
    var drawable = new RelativeConnectorsDrawable
    {
      Row = MakeRow(1, [true]),
      PhotoCenterY = 25,
      Indent = 20,
    };

    drawable.Draw(canvas.Object, new RectF(0, 0, 100, 100));

    canvas.Verify(c => c.DrawLine(10, 0, 10, 100), Times.Once());
    canvas.Verify(c => c.DrawLine(10, 25, 20, 25), Times.Once());
  }

  [Fact]
  public void An_inherited_ancestor_column_draws_a_full_height_passthrough_only_when_its_trunk_continues()
  {
    var canvas = new Mock<ICanvas>();
    var drawable = new RelativeConnectorsDrawable
    {
      Row = MakeRow(2, [true, false]),
      PhotoCenterY = 25,
      Indent = 20,
    };

    drawable.Draw(canvas.Object, new RectF(0, 0, 100, 100));

    // Column 0 (inherited ancestor trunk, continues): full-height passthrough at x = 0*20 + 10 = 10.
    canvas.Verify(c => c.DrawLine(10, 0, 10, 100), Times.Once());
    // Column 1 (this row's own trunk, does not continue past self): stops at the photo centre.
    canvas.Verify(c => c.DrawLine(30, 0, 30, 25), Times.Once());
    canvas.Verify(c => c.DrawLine(30, 25, 40, 25), Times.Once());
    canvas.Verify(c => c.DrawLine(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Exactly(3));
  }

  [Fact]
  public void An_inherited_ancestor_column_draws_nothing_once_its_trunk_has_ended()
  {
    var canvas = new Mock<ICanvas>();
    var drawable = new RelativeConnectorsDrawable
    {
      Row = MakeRow(2, [false, false]),
      PhotoCenterY = 25,
      Indent = 20,
    };

    drawable.Draw(canvas.Object, new RectF(0, 0, 100, 100));

    // Column 0's trunk already ended, so only this row's own column (column 1) draws anything.
    canvas.Verify(c => c.DrawLine(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>()), Times.Exactly(2));
    canvas.Verify(c => c.DrawLine(30, 0, 30, 25), Times.Once());
  }

  [Fact]
  public void Draw_sets_the_canvas_stroke_from_Color_and_LineWidth()
  {
    var canvas = new Mock<ICanvas>();
    var drawable = new RelativeConnectorsDrawable
    {
      Row = MakeRow(1, [false]),
      Color = Colors.Red,
      LineWidth = 3f,
    };

    drawable.Draw(canvas.Object, new RectF(0, 0, 100, 100));

    canvas.VerifySet(c => c.StrokeColor = Colors.Red, Times.Once());
    canvas.VerifySet(c => c.StrokeSize = 3f, Times.Once());
  }
}
