using GT4.UI.Resources;

namespace GT4.UI;

public static class PageAlert
{
  public static Shell CurrentShell => Shell.Current;

  public static Task<bool> ShowConfirmationAsync(string confirmationText) =>
    CurrentShell.ShowConfirmationAsync(confirmationText);

  // Alerts touch native views, so they must run on the UI thread. InvokeOnMainThreadAsync
  // runs the callback inline when we are already on the main thread, so wrapping is safe
  // for every caller and protects the background-thread call sites (Task.Run catch blocks).
  public static Task<bool> ShowConfirmationAsync(this Page page, string confirmationText) =>
    MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlertAsync(
      UIStrings.AlertTitleConfirmation,
      confirmationText,
      UIStrings.BtnNameYes,
      UIStrings.BtnNameNo));

  public static Task ShowErrorAsync(Exception exception) =>
    CurrentShell.ShowErrorAsync(exception);

  public static Task ShowErrorAsync(this Page page, Exception exception) =>
    MainThread.InvokeOnMainThreadAsync(() =>
      page.DisplayAlertAsync(UIStrings.AlertTitleError, exception.Message, UIStrings.BtnNameOk));

  public static Task ShowWarningAsync(this Page page, string message) =>
    MainThread.InvokeOnMainThreadAsync(() =>
      page.DisplayAlertAsync(UIStrings.AlertTitleWarning, message, UIStrings.BtnNameOk));
}
