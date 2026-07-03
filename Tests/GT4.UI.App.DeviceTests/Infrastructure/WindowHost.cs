namespace GT4.UI.DeviceTests;

/// <summary>
/// Modal push/pop only resolves through a Page attached to a live Window; a page constructed
/// detached (as CreatePageAsync does for the read/filter/delete tests) can't drive it. Swapping the
/// runner's own Window.Page for the duration of a test attaches the page under test without needing
/// a second top-level window. Safe because DisableTestParallelization means no other test touches
/// Windows[0] concurrently.
/// </summary>
internal static class WindowHost
{
  private sealed class Attachment(Window window, Page originalPage) : IAsyncDisposable
  {
    public ValueTask DisposeAsync() =>
      new(MainThread.InvokeOnMainThreadAsync(() => window.Page = originalPage));
  }

  public static async Task<IAsyncDisposable> AttachAsync(Page page)
  {
    var window = Application.Current!.Windows[0];
    var originalPage = window.Page!;

    await MainThread.InvokeOnMainThreadAsync(() => window.Page = page);

    return new Attachment(window, originalPage);
  }
}
