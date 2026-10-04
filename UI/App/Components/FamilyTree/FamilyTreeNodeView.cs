using GT4.UI.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Maui.Controls.Shapes;

namespace GT4.UI.Components.Genealogy;

/// <summary>
/// A single family-tree node: a circular photo with the person's name underneath. The centred
/// person is emphasised with a thicker, primary-coloured ring. The photo is supplied already resolved
/// (and downsized) by the page so this view holds only a small thumbnail, not a full-resolution bitmap.
/// </summary>
public sealed class FamilyTreeNodeView : ContentView
{
  private const double BorderThikness = 1.5;
  private const double CenterBorderThikness = 3;
  private const double PhotoSizeBase = 60;
  private const double FontSizeBase = 12;
  private const double SpacingBase = 4;
  private const double HighlightedRingFactor = 2;

  private readonly Border _Ring;
  private readonly double _PhotoSize;
  private readonly double _RingThickness;

  public FamilyTreeNodeView(
    ImageSource photo,
    FontScale? fontScale,
    string displayName,
    bool isCenter,
    double width,
    double height,
    double zoomScale = 1.0
  )
  {
    WidthRequest = width;
    HeightRequest = height;

    var photoSize = PhotoSizeBase * zoomScale;
    var ringColor = ThemedColor.Resolve(isCenter ? "Primary" : "Accent", isCenter ? Colors.DarkGreen : Color.FromArgb("#8B6F4E"));
    _PhotoSize = photoSize;
    _RingThickness = (isCenter ? CenterBorderThikness : BorderThikness) * zoomScale;

    var image = new Image
    {
      Source = photo,
      Aspect = Aspect.AspectFill,
      WidthRequest = photoSize,
      HeightRequest = photoSize,
      Clip = new EllipseGeometry(new Point(photoSize / 2, photoSize / 2), photoSize / 2, photoSize / 2),
    };

    _Ring = new Border
    {
      Padding = 0,
      Stroke = ringColor,
      StrokeShape = new Ellipse(),
      HorizontalOptions = LayoutOptions.Center,
      Content = image,
    };
    SetHighlighted(false);

    var name = new Label
    {
      Text = displayName,
      FontSize = FontSizeBase * zoomScale * (fontScale?.CurrentFactor ?? FontScale.DefaultFactor),
      FontAttributes = isCenter ? FontAttributes.Bold : FontAttributes.None,
      HorizontalTextAlignment = TextAlignment.Center,
      MaxLines = 2,
      LineBreakMode = LineBreakMode.TailTruncation,
      HorizontalOptions = LayoutOptions.Center,
    };

    Content = new VerticalStackLayout
    {
      Spacing = SpacingBase * zoomScale,
      HorizontalOptions = LayoutOptions.Center,
      Children = { _Ring, name },
    };

    if (fontScale is not null)
    {
      void OnFontScaleChanged(object? sender, EventArgs e) =>
        name.FontSize = FontSizeBase * zoomScale * fontScale.CurrentFactor;

      fontScale.Changed += OnFontScaleChanged;
      Unloaded += (_, _) => fontScale.Changed -= OnFontScaleChanged;
    }
  }

  // A heavier ring grows outward over a negative margin, so the photo and the name under it stay put.
  public void SetHighlighted(bool isHighlighted)
  {
    var thickness = isHighlighted ? _RingThickness * HighlightedRingFactor : _RingThickness;
    _Ring.StrokeThickness = thickness;
    _Ring.WidthRequest = _PhotoSize + (thickness * 2);
    _Ring.HeightRequest = _PhotoSize + (thickness * 2);
    _Ring.Margin = _RingThickness - thickness;
  }
}
