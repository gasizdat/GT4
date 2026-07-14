namespace GT4.UI.Components;

/// <summary>
/// Strokes one relatives-tree row's connector lines onto its <see cref="GraphicsView"/>: a full-height
/// pass-through for every ancestor column whose sibling trunk continues, plus this row's own elbow
/// (vertical down to the photo centre, horizontal across to the content, and on past the centre when
/// the row has a later sibling). Draws nothing while <see cref="RelativeRow.IsFilterActive"/> is set,
/// since a filtered tree's hidden rows would make the lines inaccurate. Every coordinate derives from
/// <see cref="Row"/> and <see cref="PhotoCenterY"/>, so a recycled row can never keep another node's
/// lines.
/// </summary>
public sealed class RelativeConnectorsDrawable : IDrawable
{
  public RelativeRow? Row { get; set; }

  /// <summary>Vertical centre of the photo within the row; the elbow meets the content here.</summary>
  public double PhotoCenterY { get; set; }

  public double Indent { get; set; }

  public Color Color { get; set; } = Colors.Gray;

  public float LineWidth { get; set; } = 1f;

  public void Draw(ICanvas canvas, RectF dirtyRect)
  {
    var row = Row;
    if (row is null || row.Depth == 0 || row.IsFilterActive)
    {
      return;
    }

    canvas.StrokeColor = Color;
    canvas.StrokeSize = LineWidth;

    var half = (float)(Indent / 2);
    var centerY = (float)PhotoCenterY;
    var ownColumn = row.Depth - 1;

    for (var k = 0; k < row.Depth; k++)
    {
      var x = (float)(k * Indent) + half;
      if (k < ownColumn)
      {
        // Inherited ancestor trunk passing straight through this row.
        if (row.AncestorContinues[k])
        {
          canvas.DrawLine(x, 0, x, dirtyRect.Height);
        }
      }
      else
      {
        // This row's own trunk: down to the photo centre, continuing to the bottom only when a later
        // sibling follows, plus the horizontal stub reaching the content.
        var bottom = row.AncestorContinues[k] ? dirtyRect.Height : centerY;
        canvas.DrawLine(x, 0, x, bottom);
        canvas.DrawLine(x, centerY, (float)(row.Depth * Indent), centerY);
      }
    }
  }
}
