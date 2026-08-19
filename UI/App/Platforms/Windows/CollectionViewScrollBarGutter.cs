using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Items;

namespace GT4.UI;

// WinUI overlays CollectionView's native scrollbar on top of item content instead of reserving its
// own gutter, and widens further on hover/press (dotnet/maui#32355). Padding the platform view on
// the scroll-axis edge reserves that space -- confirmed empirically that the scrollbar's own lane is
// independent of Padding, so this doesn't just shift the scrollbar along with the content.
static class CollectionViewScrollBarGutter
{
  public static void Register() =>
      CollectionViewHandler.Mapper.AppendToMapping(nameof(CollectionViewScrollBarGutter), Apply);

  static void Apply(CollectionViewHandler handler, CollectionView view)
  {
    var isHorizontal = (view.ItemsLayout as ItemsLayout)?.Orientation == ItemsLayoutOrientation.Horizontal;
    var gutter = ScrollBarGutterSize.Resolve();
    handler.PlatformView.Padding = isHorizontal
        ? new Microsoft.UI.Xaml.Thickness(0, 0, 0, gutter)
        : new Microsoft.UI.Xaml.Thickness(0, 0, gutter, 0);
  }
}
