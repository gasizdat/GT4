#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Asks WinUI which native elements take the pointer at a point, which is what an input-transparent
/// overlay has to stay out of. Main thread only, on a page attached to the window.
/// </summary>
internal static class WinUIHitTest
{
  // The point is in the origin's own units.
  public static UIElement[] At(VisualElement origin, Point at)
  {
    var native = Native(origin);
    var transform = native.TransformToVisual(null);
    var host = transform.TransformPoint(new Windows.Foundation.Point(at.X, at.Y));
    var hits = VisualTreeHelper.FindElementsInHostCoordinates(host, native.XamlRoot.Content);
    return [.. hits];
  }

  public static UIElement Native(VisualElement element) => (UIElement)element.Handler!.PlatformView!;
}
#endif
