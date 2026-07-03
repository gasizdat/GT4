namespace GT4.UI.DeviceTests;

/// <summary>
/// Starts a Task-returning action on the UI thread without blocking on its completion. Needed for
/// actions like OnAddName that call into WinUI navigation (PushModalAsync) -- those calls fail with a
/// COMException if made from the test's own (non-UI) thread, but the caller must not await the
/// result here, since the action won't complete until a later step (e.g. driving the pushed dialog).
/// </summary>
internal static class MainThreadTask
{
  public static async Task<Task> StartAsync(Func<Task> action)
  {
    var started = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
    MainThread.BeginInvokeOnMainThread(() => started.SetResult(action()));
    return await started.Task;
  }
}
