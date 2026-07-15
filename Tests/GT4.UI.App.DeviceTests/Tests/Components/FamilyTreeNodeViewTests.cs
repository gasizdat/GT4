using GT4.UI.Components.Genealogy;
using GT4.UI.Utils.Settings;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers FamilyTreeNodeView's code-built visual tree directly: the photo/ring/label sizing all scale
/// with zoomScale, the centred node gets a thicker, differently-coloured ring and a bold label, and the
/// label's font size additionally honours the injected FontScale (falling back to FontScale.DefaultFactor
/// when null, since standalone usages outside the family tree page may not have one). GetColor's resource
/// lookup is exercised against the app's real merged Colors.xaml (via TestStyles.EnsureLoaded), not a
/// stub, since that's the actual lookup this view depends on.
/// </summary>
public class FamilyTreeNodeViewTests
{
  private static async Task<FamilyTreeNodeView> CreateNodeAsync(
    bool isCenter = false,
    double zoomScale = 1.0,
    FontScale? fontScale = null,
    string displayName = "Jane Doe")
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() =>
      new FamilyTreeNodeView(ImageSource.FromFile("dummy.png"), fontScale, displayName, isCenter, width: 100, height: 120, zoomScale));
  }

  private static (Border Ring, Label Name) GetParts(FamilyTreeNodeView node)
  {
    var stack = Assert.IsType<VerticalStackLayout>(node.Content);
    var ring = Assert.IsType<Border>(stack.Children[0]);
    var name = Assert.IsType<Label>(stack.Children[1]);
    return (ring, name);
  }

  [Fact]
  public async Task WidthRequest_and_HeightRequest_come_straight_from_the_constructor()
  {
    var node = await CreateNodeAsync();

    Assert.Equal(100, node.WidthRequest);
    Assert.Equal(120, node.HeightRequest);
  }

  [Fact]
  public async Task The_photo_is_clipped_to_a_circle_sized_from_zoomScale()
  {
    var node = await CreateNodeAsync(zoomScale: 2.0);

    var (ring, _) = GetParts(node);
    var image = Assert.IsType<Image>(ring.Content);
    Assert.Equal(Aspect.AspectFill, image.Aspect);
    Assert.Equal(120, image.WidthRequest);
    Assert.Equal(120, image.HeightRequest);
    Assert.IsType<EllipseGeometry>(image.Clip);
  }

  [Fact]
  public async Task A_non_center_node_gets_the_thinner_Accent_ring()
  {
    var node = await CreateNodeAsync(isCenter: false, zoomScale: 1.0);

    var (ring, _) = GetParts(node);
    Assert.Equal(1.5, ring.StrokeThickness);
    Assert.Equal(Color.FromArgb("#8B6F4E"), ring.Stroke);
    Assert.Equal(60 + 1.5 * 2, ring.WidthRequest);
  }

  [Fact]
  public async Task A_center_node_gets_a_thicker_Primary_ring()
  {
    var node = await CreateNodeAsync(isCenter: true, zoomScale: 1.0);

    var (ring, _) = GetParts(node);
    Assert.Equal(3, ring.StrokeThickness);
    Assert.Equal(Color.FromArgb("#1E4437"), ring.Stroke);
    Assert.Equal(60 + 3 * 2, ring.WidthRequest);
  }

  [Fact]
  public async Task Ring_thickness_scales_with_zoomScale()
  {
    var node = await CreateNodeAsync(isCenter: true, zoomScale: 2.0);

    var (ring, _) = GetParts(node);
    Assert.Equal(6, ring.StrokeThickness);
  }

  [Fact]
  public async Task A_non_center_nodes_ring_thickness_also_scales_with_zoomScale()
  {
    var node = await CreateNodeAsync(isCenter: false, zoomScale: 2.0);

    var (ring, _) = GetParts(node);
    Assert.Equal(3, ring.StrokeThickness);
  }

  [Fact]
  public async Task The_label_shows_the_display_name_and_wraps_up_to_two_lines()
  {
    var node = await CreateNodeAsync(displayName: "Jane Doe");

    var (_, name) = GetParts(node);
    Assert.Equal("Jane Doe", name.Text);
    Assert.Equal(2, name.MaxLines);
    Assert.Equal(LineBreakMode.TailTruncation, name.LineBreakMode);
  }

  [Fact]
  public async Task A_center_nodes_label_is_bold_a_non_center_nodes_is_not()
  {
    var center = await CreateNodeAsync(isCenter: true);
    var notCenter = await CreateNodeAsync(isCenter: false);

    Assert.Equal(FontAttributes.Bold, GetParts(center).Name.FontAttributes);
    Assert.Equal(FontAttributes.None, GetParts(notCenter).Name.FontAttributes);
  }

  [Fact]
  public async Task The_label_font_size_scales_with_zoomScale_and_defaults_to_FontScale_DefaultFactor()
  {
    var node = await CreateNodeAsync(zoomScale: 2.0, fontScale: null);

    var (_, name) = GetParts(node);
    Assert.Equal(12 * 2.0 * FontScale.DefaultFactor, name.FontSize);
  }

  [Fact]
  public async Task The_label_font_size_honours_an_injected_FontScale()
  {
    var fontScale = new FontScale();
    await MainThread.InvokeOnMainThreadAsync(() => fontScale.Apply(1.5));

    var node = await CreateNodeAsync(zoomScale: 1.0, fontScale: fontScale);

    var (_, name) = GetParts(node);
    Assert.Equal(12 * 1.0 * 1.5, name.FontSize);
  }

  [Fact]
  public async Task The_label_font_size_updates_live_when_FontScale_changes()
  {
    var fontScale = new FontScale();
    var node = await CreateNodeAsync(zoomScale: 1.0, fontScale: fontScale);

    await MainThread.InvokeOnMainThreadAsync(() => fontScale.Apply(1.5));

    var (_, name) = GetParts(node);
    Assert.Equal(12 * 1.0 * 1.5, name.FontSize);
  }

  [Fact]
  public async Task Stack_spacing_scales_with_zoomScale()
  {
    var node = await CreateNodeAsync(zoomScale: 3.0);

    var stack = Assert.IsType<VerticalStackLayout>(node.Content);
    Assert.Equal(4 * 3.0, stack.Spacing);
  }
}
