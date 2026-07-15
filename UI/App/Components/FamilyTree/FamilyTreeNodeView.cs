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
    var ringColor = GetColor(isCenter ? "Primary" : "Accent", isCenter ? Colors.DarkGreen : Color.FromArgb("#8B6F4E"));
    var borderThikness = (isCenter ? CenterBorderThikness : BorderThikness) * zoomScale;

    var image = new Image
    {
      Source = photo,
      Aspect = Aspect.AspectFill,
      WidthRequest = photoSize,
      HeightRequest = photoSize,
      Clip = new EllipseGeometry(new Point(photoSize / 2, photoSize / 2), photoSize / 2, photoSize / 2),
    };

    var ring = new Border
    {
      WidthRequest = photoSize + borderThikness * 2,
      HeightRequest = photoSize + borderThikness * 2,
      Padding = 0,
      Stroke = ringColor,
      StrokeThickness = borderThikness,
      StrokeShape = new Ellipse(),
      HorizontalOptions = LayoutOptions.Center,
      Content = image,
    };

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
      Children = { ring, name },
    };

    if (fontScale is not null)
    {
      void OnFontScaleChanged(object? sender, EventArgs e) =>
        name.FontSize = FontSizeBase * zoomScale * fontScale.CurrentFactor;

      fontScale.Changed += OnFontScaleChanged;
      Unloaded += (_, _) => fontScale.Changed -= OnFontScaleChanged;
    }
  }

  private static Color GetColor(string resourceKey, Color fallback) =>
    Application.Current?.Resources is { } resources
    && resources.TryGetValue(resourceKey, out var value)
    && value is Color color
      ? color
      : fallback;
}
