#if WINDOWS
using Xunit;

namespace GT4.UI.DeviceTests;

// Pins the ScrollView half of issue #325's follow-up: the same WinUI overlay-scrollbar problem as
// CollectionView (dotnet/maui#32355), fixed by GT4.UI.ScrollViewScrollBarGutter reserving a native
// gutter sized to WinUI's ScrollBarSize resource (12 in the current Windows App SDK). Only the two
// orientations actually used in GT4 (Vertical -- the default -- and Both, on FamilyTreePage's
// pannable canvas) are pinned; Horizontal/Neither aren't used anywhere in the app.
public class ScrollViewScrollBarGutterTests
{
  private static async Task<Microsoft.UI.Xaml.Thickness> PaddingForAsync(ScrollOrientation orientation)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var scrollView = new ScrollView { Content = new Label { Text = "x" }, Orientation = orientation };
    var page = new ContentPage { Content = scrollView };
    await using var window = await WindowHost.AttachAsync(page);

    if (scrollView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.ScrollViewer native)
    {
      throw new InvalidOperationException("No native ScrollViewer.");
    }
    return await MainThread.InvokeOnMainThreadAsync(() => native.Padding);
  }

  [Fact]
  public async Task Vertical_ScrollView_pads_the_right_edge()
  {
    var padding = await PaddingForAsync(ScrollOrientation.Vertical);

    Assert.Equal(new Microsoft.UI.Xaml.Thickness(0, 0, 12, 0), padding);
  }

  [Fact]
  public async Task Both_orientation_ScrollView_pads_both_trailing_edges()
  {
    var padding = await PaddingForAsync(ScrollOrientation.Both);

    Assert.Equal(new Microsoft.UI.Xaml.Thickness(0, 0, 12, 12), padding);
  }
}
#endif
