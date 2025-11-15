using GT4.UI.Resources;

namespace GT4.UI;

public static class PageAlert
{
  public static Shell CurrentShell => Shell.Current;

  public static Task<bool> ShowConfirmation(string confirmationText) =>
    CurrentShell.ShowConfirmation(confirmationText);

  public static Task<bool> ShowConfirmation(this Page page, string confirmationText) =>
    CurrentShell.DisplayAlert(
      UIStrings.AlertTitleConfirmation,
      confirmationText,
      UIStrings.BtnNameYes,
      UIStrings.BtnNameNo);

  public static Task ShowError(Exception exception) =>
    CurrentShell.ShowError(exception);

  public static Task ShowError(this Page page, Exception exception) =>
    page.DisplayAlert(UIStrings.AlertTitleError, exception.Message, UIStrings.BtnNameOk);
}
