using System.Runtime.InteropServices;

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
    public ValueTask DisposeAsync() => new(MainThread.InvokeOnMainThreadAsync(RestoreOriginalPageAsync));

    // Restoring the runner's own page rebuilds its native WinUI visual tree (styles, resources)
    // right after the test page's tree was torn down. That churn has occasionally raced a WinRT
    // interop call into a spurious COMException (observed as HRESULT 0x800F1000 out of
    // UIElementCollection.Add) -- not reproducible on demand, so absorb it with a short retry
    // rather than failing the test over a restore-step platform race.
    private async Task RestoreOriginalPageAsync()
    {
      for (var attempt = 1; ; attempt++)
      {
        try
        {
          window.Page = originalPage;
          return;
        }
        catch (COMException) when (attempt < 3)
        {
          await Task.Delay(50);
        }
      }
    }
  }

  public static async Task<IAsyncDisposable> AttachAsync(Page page)
  {
    var window = Application.Current!.Windows[0];
    var originalPage = window.Page!;

    await MainThread.InvokeOnMainThreadAsync(() => window.Page = page);

    return new Attachment(window, originalPage);
  }
}
