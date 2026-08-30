using Android.Views;
using GT4.UI.Components;
using Microsoft.Maui.Handlers;

namespace GT4.UI;

// Android resolves a pointer icon the same way WinUI resolves a cursor -- by walking up from the view
// under the pointer -- so the page's own view group covers the whole page. Nothing is drawn on a device
// with no pointer attached; this shows only in DeX, on a Chromebook or with a mouse plugged in.
static class PageLayoutWaitCursor
{
  public static void Register(IMauiHandlersCollection handlers) =>
    handlers.AddHandler<PageLayout, PageLayoutHandler>();
}

sealed class PageLayoutHandler : ContentViewHandler
{
  private static readonly IPropertyMapper<PageLayout, PageLayoutHandler> WaitCursorMapper =
    new PropertyMapper<PageLayout, PageLayoutHandler>(Mapper)
    {
      [nameof(PageLayout.Loading)] = MapLoading
    };

  public PageLayoutHandler()
    : base(WaitCursorMapper)
  {
  }

  // View.PointerIcon arrived in API 24; the app still supports 21.
  private static void MapLoading(PageLayoutHandler handler, PageLayout view)
  {
    if (!OperatingSystem.IsAndroidVersionAtLeast(24))
    {
      return;
    }

    var platformView = handler.PlatformView;
    platformView.PointerIcon = view.Loading?.IsBusy == true
      ? PointerIcon.GetSystemIcon(platformView.Context!, PointerIconType.Wait)
      : null;
  }
}
