using GT4.UI.Resources;

namespace GT4.UI;

public static class PageAlert
{
  public static Shell CurrentShell => Shell.Current;

  public static Task<bool> ShowConfirmationAsync(string confirmationText) =>
    CurrentShell.ShowConfirmationAsync(confirmationText);

  public static Task<bool> ShowConfirmationAsync(this Page page, string confirmationText) =>
    CurrentShell.DisplayAlertAsync(
      UIStrings.AlertTitleConfirmation,
      confirmationText,
      UIStrings.BtnNameYes,
      UIStrings.BtnNameNo);

  public static Task ShowErrorAsync(Exception exception) =>
    CurrentShell.ShowErrorAsync(exception);

  public static Task ShowErrorAsync(this Page page, Exception exception) =>
    page.DisplayAlertAsync(UIStrings.AlertTitleError, exception.Message, UIStrings.BtnNameOk);
}
