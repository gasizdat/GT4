using GT4.UI.Components;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Input;

namespace GT4.UI;

// WinUI declares UIElement.ProtectedCursor protected, so a cursor can only be assigned from inside the
// element's own class -- hence a ContentPanel subclass MAUI is told to build for PageLayout. WinUI
// resolves the cursor by walking up from the element under the pointer, so the page's own panel covers
// every child that does not set one itself (an Entry keeps its I-beam).
static class PageLayoutWaitCursor
{
  public static void Register(IMauiHandlersCollection handlers) =>
    handlers.AddHandler<PageLayout, PageLayoutHandler>();
}

sealed class WaitCursorPanel : ContentPanel
{
  // WinUI keeps the getter protected too, so this is the only way a test can read the cursor back.
  public InputCursor? CurrentCursor => ProtectedCursor;

  public void SetWaitCursor(bool isBusy) =>
    ProtectedCursor = isBusy ? InputSystemCursor.Create(InputSystemCursorShape.Wait) : null;
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

  protected override ContentPanel CreatePlatformView() =>
    new WaitCursorPanel { CrossPlatformLayout = VirtualView };

  private static void MapLoading(PageLayoutHandler handler, PageLayout view)
  {
    var panel = (WaitCursorPanel)handler.PlatformView;
    panel.SetWaitCursor(view.Loading?.IsBusy == true);
  }
}
