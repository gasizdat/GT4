using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using Microsoft.Maui.Controls.Shapes;

namespace GT4.UI.Components.Genealogy;

/// <summary>
/// A single family-tree node: a circular photo with the person's name underneath. The centred
/// person is emphasised with a thicker, primary-coloured ring.
/// </summary>
public sealed class FamilyTreeNodeView : ContentView
{
  private const double PhotoSize = 60;

  public FamilyTreeNodeView(PersonInfo person, string displayName, bool isCenter, double width, double height)
  {
    WidthRequest = width;
    HeightRequest = height;

    var ringColor = GetColor(isCenter ? "Primary" : "Accent", isCenter ? Colors.DarkGreen : Color.FromArgb("#8B6F4E"));

    var photo = new Image
    {
      Source = ResolvePhoto(person),
      Aspect = Aspect.AspectFill,
      WidthRequest = PhotoSize,
      HeightRequest = PhotoSize,
      Clip = new EllipseGeometry(new Point(PhotoSize / 2, PhotoSize / 2), PhotoSize / 2, PhotoSize / 2),
    };

    var ring = new Border
    {
      WidthRequest = PhotoSize,
      HeightRequest = PhotoSize,
      Padding = 0,
      Stroke = ringColor,
      StrokeThickness = isCenter ? 3 : 1.5,
      StrokeShape = new Ellipse(),
      HorizontalOptions = LayoutOptions.Center,
      Content = photo,
    };

    var name = new Label
    {
      Text = displayName,
      FontSize = 12,
      FontAttributes = isCenter ? FontAttributes.Bold : FontAttributes.None,
      HorizontalTextAlignment = TextAlignment.Center,
      MaxLines = 2,
      LineBreakMode = LineBreakMode.TailTruncation,
      HorizontalOptions = LayoutOptions.Center,
    };

    Content = new VerticalStackLayout
    {
      Spacing = 4,
      HorizontalOptions = LayoutOptions.Center,
      Children = { ring, name },
    };
  }

  private static ImageSource ResolvePhoto(PersonInfo person) =>
    person.MainPhoto is { Content.Length: > 0 } photo
      ? ImageUtils.ImageFromBytes(photo.Content)
      : ImageUtils.ImageFromRawResource(DefaultPhotoResource(person.BiologicalSex));

  private static string DefaultPhotoResource(BiologicalSex sex) => sex switch
  {
    BiologicalSex.Male => "male_stub.png",
    BiologicalSex.Female => "female_stub.png",
    _ => "project_icon.png",
  };

  private static Color GetColor(string resourceKey, Color fallback) =>
    Application.Current?.Resources is { } resources
    && resources.TryGetValue(resourceKey, out var value)
    && value is Color color
      ? color
      : fallback;
}
