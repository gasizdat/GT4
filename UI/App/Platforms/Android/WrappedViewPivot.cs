using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace GT4.UI;

// MAUI applies a view's initial rotation and scale to its own native view, pivoted on the frame it
// has before any layout (-1 wide), but each layout re-pivots only the container that wraps an
// input-transparent, clipped or shadowed view. Left alone, such a view rotated from the start turns
// about its corner instead (the fan chart's labels, wrapped by their input-transparent layer).
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
