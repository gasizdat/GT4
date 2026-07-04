namespace GT4.UI.DeviceTests;

/// <summary>
/// Waits for a modal pushed by the page under test to appear on its ModalStack. Needed because the
/// caller has no direct reference to the dialog instance -- it's a local inside OnAddName /
/// CreateOrUpdateNameDialog.UpdateNameAsync, only reachable through the Navigation it was pushed on.
/// </summary>
internal static class ModalDialogHarness
{
  public static async Task<TDialog> WaitForModalAsync<TDialog>(Page hostPage, TimeSpan? timeout = null)
    where TDialog : Page
  {
    var modal = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => hostPage.Navigation.ModalStack.LastOrDefault() as TDialog),
      m => m is not null,
      timeout,
      $"No {typeof(TDialog).Name} was pushed onto {hostPage.GetType().Name}'s modal stack.");
    return modal!;
  }
}
