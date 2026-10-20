using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace GT4.UI;

// MAUI applies a view's initial transforms to its own native view, pivoted on its pre-layout frame
// (-1 wide), but each layout re-pivots only the container wrapping an input-transparent, clipped or
// shadowed view, which would otherwise rotate or scale about its corner.
static class WrappedViewPivot
{
  public static void Register() =>
      ViewHandler.ViewCommandMapper.AppendToMapping(nameof(IView.Frame), Apply);

  static void Apply(IViewHandler handler, IView view, object? args)
  {
    if (!handler.HasContainer)
    {
      return;
    }

    var platformView = (Android.Views.View)handler.PlatformView!;
    platformView.UpdateAnchorX(view);
    platformView.UpdateAnchorY(view);
  }
}
