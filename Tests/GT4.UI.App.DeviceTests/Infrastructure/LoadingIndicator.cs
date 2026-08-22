using GT4.UI.Components;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Reaches the shared activity indicator that PageLayout hosts for every page and dialog. Read
/// <see cref="PageLayout.IsLoading"/> inside the same UI-thread dispatch that triggers the load:
/// PageLayout.RunLoad sets it synchronously, so the load's completion callback -- itself posted to
/// the UI thread -- cannot interleave and turn a positive assertion flaky.
/// </summary>
internal static class LoadingIndicator
{
  public static PageLayout Of(Element page) => page.FindByName<PageLayout>("LayoutView");

  public static Task WaitUntilHiddenAsync(PageLayout layout, string timeoutMessage) =>
    Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => layout.IsLoading),
      isLoading => !isLoading,
      TimeSpan.FromSeconds(10),
      timeoutMessage);
}
