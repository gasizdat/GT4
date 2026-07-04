using GT4.UI.Resources;

namespace GT4.UI;

public interface IPageAlertService
{
  Task<bool> ShowConfirmationAsync(Page page, string confirmationText);
  Task ShowErrorAsync(Page page, Exception exception);
  Task ShowErrorAsync(Exception exception);
  Task ShowWarningAsync(Page page, string message);
}

// Alerts touch native views, so they must run on the UI thread. InvokeOnMainThreadAsync
// runs the callback inline when we are already on the main thread, so wrapping is safe
// for every caller and protects the background-thread call sites (Task.Run catch blocks).
internal sealed class RealPageAlertService : IPageAlertService
{
  public Task<bool> ShowConfirmationAsync(Page page, string confirmationText) =>
    MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlertAsync(
      UIStrings.AlertTitleConfirmation,
      confirmationText,
      UIStrings.BtnNameYes,
      UIStrings.BtnNameNo));

  public Task ShowErrorAsync(Page page, Exception exception) =>
    MainThread.InvokeOnMainThreadAsync(() =>
      page.DisplayAlertAsync(UIStrings.AlertTitleError, exception.Message, UIStrings.BtnNameOk));

  public Task ShowErrorAsync(Exception exception) => ShowErrorAsync(Shell.Current, exception);

  public Task ShowWarningAsync(Page page, string message) =>
    MainThread.InvokeOnMainThreadAsync(() =>
      page.DisplayAlertAsync(UIStrings.AlertTitleWarning, message, UIStrings.BtnNameOk));
}
