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
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));

    while (DateTime.UtcNow < deadline)
    {
      var modal = await MainThread.InvokeOnMainThreadAsync(() => hostPage.Navigation.ModalStack.LastOrDefault());
      if (modal is TDialog dialog)
      {
        return dialog;
      }

      await Task.Delay(20);
    }

    throw new TimeoutException($"No {typeof(TDialog).Name} was pushed onto {hostPage.GetType().Name}'s modal stack.");
  }
}
