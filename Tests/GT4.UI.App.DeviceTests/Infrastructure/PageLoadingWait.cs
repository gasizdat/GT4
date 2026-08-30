namespace GT4.UI.DeviceTests;

internal static class PageLoadingWait
{
  public static Task UntilIdleAsync(this PageLoading loading, string timeoutMessage) =>
    Poll.UntilAsync(
      () => Task.FromResult(loading.IsBusy),
      isBusy => !isBusy,
      TimeSpan.FromSeconds(10),
      timeoutMessage);
}
