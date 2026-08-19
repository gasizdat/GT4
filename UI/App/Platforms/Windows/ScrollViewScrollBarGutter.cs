using Microsoft.Maui.Handlers;

namespace GT4.UI;

// Same WinUI overlay-scrollbar problem as CollectionViewScrollBarGutter (dotnet/maui#32355), for the
// other native control that hosts a scrollbar on Windows: ScrollView wraps a Microsoft.UI.Xaml.Controls
// .ScrollViewer directly, which is the same control class CollectionView's ListViewBase wraps
// internally, so the same Padding-reserves-a-gutter mechanism applies.
static class ScrollViewScrollBarGutter
{
  public static void Register() =>
      ScrollViewHandler.Mapper.AppendToMapping(nameof(ScrollViewScrollBarGutter), Apply);

  static void Apply(IScrollViewHandler handler, IScrollView view)
  {
    var gutter = ScrollBarGutterSize.Resolve();
    var (right, bottom) = view.Orientation switch
    {
      ScrollOrientation.Horizontal => (0, gutter),
      ScrollOrientation.Both => (gutter, gutter),
      ScrollOrientation.Neither => (0, 0),
      _ => (gutter, 0), // Vertical, and ScrollView's default.
    };
    var native = (Microsoft.UI.Xaml.Controls.ScrollViewer)handler.PlatformView;
    native.Padding = new Microsoft.UI.Xaml.Thickness(0, 0, right, bottom);
  }
}
