namespace GT4.UI.DeviceTests;

internal static class PageLoadingWait
{
  public static Task UntilIdleAsync(this PageLoading loading, string timeoutMessage) =>
    Poll.UntilAsync(
      () => Task.FromResult(loading.IsLoading),
      isLoading => !isLoading,
      TimeSpan.FromSeconds(10),
      timeoutMessage);
}
