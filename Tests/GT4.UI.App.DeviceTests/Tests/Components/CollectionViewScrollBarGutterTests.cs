#if WINDOWS
using Xunit;

namespace GT4.UI.DeviceTests;

// Pins issue #325: GT4.UI.CollectionViewScrollBarGutter reserves a native gutter on the
// CollectionView's scroll-axis edge, sized to WinUI's ScrollBarSize resource (12 in the current
// Windows App SDK), so its overlay scrollbar stops covering trailing-edge content (e.g. NamesPage's
// Delete/Edit buttons).
public class CollectionViewScrollBarGutterTests
{
  private static async Task<Microsoft.UI.Xaml.Thickness> PaddingForAsync(IItemsLayout? itemsLayout)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var collectionView = new CollectionView { ItemsSource = Array.Empty<int>() };
    if (itemsLayout is not null) { collectionView.ItemsLayout = itemsLayout; }

    var page = new ContentPage { Content = collectionView };
    await using var window = await WindowHost.AttachAsync(page);

    if (collectionView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.ListViewBase native)
    {
      throw new InvalidOperationException("No native ListViewBase.");
    }
    return await MainThread.InvokeOnMainThreadAsync(() => native.Padding);
  }

  [Fact]
  public async Task Vertical_CollectionView_pads_the_right_edge()
  {
    var padding = await PaddingForAsync(null);

    Assert.Equal(new Microsoft.UI.Xaml.Thickness(0, 0, 12, 0), padding);
  }

  [Fact]
  public async Task Horizontal_CollectionView_pads_the_bottom_edge()
  {
    var padding = await PaddingForAsync(LinearItemsLayout.Horizontal);

    Assert.Equal(new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12), padding);
  }
}
#endif
