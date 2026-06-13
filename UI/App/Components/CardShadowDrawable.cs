namespace GT4.UI.Components;

/// <summary>
/// Draws a soft drop shadow for a rounded card that occupies the drawing area
/// minus <see cref="ShadowSpace"/> on the right and bottom. The card region is
/// clipped out, so the shadow is painted only outside the card and can never
/// show through a translucent card background.
/// </summary>
public class CardShadowDrawable : IDrawable
{
  public float ShadowSpace { get; set; } = 8f;
  
  public float ShadowBlure { get; set; } = 8f;

  public Color ShadowColor { get; set; } = Color.FromArgb("#40000000");

  public float CornerRadius { get; set; } = 10f;


  public void Draw(ICanvas canvas, RectF dirtyRect)
  {
    var card = new RectF(
      dirtyRect.X,
      dirtyRect.Y,
      dirtyRect.Width - ShadowSpace,
      dirtyRect.Height - ShadowSpace);

    var cardPath = new PathF();
    cardPath.AppendRoundedRectangle(card, CornerRadius);

    var outsideCard = new PathF();
    outsideCard.AppendRectangle(dirtyRect);
    outsideCard.AppendRoundedRectangle(card, CornerRadius);
    canvas.ClipPath(outsideCard, WindingMode.EvenOdd);

    canvas.SetShadow(new SizeF(ShadowSpace/2f, ShadowSpace/2f), ShadowBlure, ShadowColor);
    canvas.FillColor = Colors.Black;
    canvas.FillPath(cardPath);
  }
}
