using GT4.UI.Resources;

namespace GT4.UI;

public interface IAlertService
{
  Task<bool> ShowConfirmationAsync(string confirmationText);
  Task ShowErrorAsync(Exception exception);
  Task ShowWarningAsync(string message);
}

// Alerts touch native views, so they must run on the UI thread. InvokeOnMainThreadAsync
// runs the callback inline when we are already on the main thread, so wrapping is safe
// for every caller and protects the background-thread call sites (Task.Run catch blocks).
internal sealed class RealAlertService : IAlertService
{
  private Shell CurrentShell => Shell.Current;

  public Task<bool> ShowConfirmationAsync(string confirmationText) =>
    MainThread.InvokeOnMainThreadAsync(() => CurrentShell.DisplayAlertAsync(
      UIStrings.AlertTitleConfirmation,
      confirmationText,
      UIStrings.BtnNameYes,
      UIStrings.BtnNameNo));

  public Task ShowErrorAsync(Exception exception) =>
    MainThread.InvokeOnMainThreadAsync(() =>
      CurrentShell.DisplayAlertAsync(UIStrings.AlertTitleError, exception.Message, UIStrings.BtnNameOk));

  public Task ShowWarningAsync(string message) =>
    MainThread.InvokeOnMainThreadAsync(() =>
      CurrentShell.DisplayAlertAsync(UIStrings.AlertTitleWarning, message, UIStrings.BtnNameOk));
}
