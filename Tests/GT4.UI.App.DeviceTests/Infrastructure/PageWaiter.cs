using System.ComponentModel;
using GT4.Core.Project.Dto;
using GT4.UI.Pages;

namespace GT4.UI.DeviceTests;

internal static class PageWaiter
{
  /// <summary>
  /// Runs <paramref name="interact"/> on the UI thread, triggers a Names reload, and waits for the
  /// reload to complete (signalled by the page's own PropertyChanged(CurrentName) at the end of its
  /// background-load pipeline), then returns the reloaded items.
  /// </summary>
  public static async Task<Name[]> ReloadNamesAsync(NamesPage page, Action? interact = null, TimeSpan? timeout = null)
  {
    var loadCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName == nameof(NamesPage.CurrentName))
      {
        loadCompleted.TrySetResult();
      }
    }

    page.PropertyChanged += OnPropertyChanged;
    try
    {
      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        interact?.Invoke();
        _ = page.Names;
      });

      var completed = await Task.WhenAny(loadCompleted.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(10)));
      if (completed != loadCompleted.Task)
      {
        throw new TimeoutException("Names reload did not complete; check the TestServices mock setup.");
      }

      return await MainThread.InvokeOnMainThreadAsync(() => page.Names.ToArray());
    }
    finally
    {
      page.PropertyChanged -= OnPropertyChanged;
    }
  }
}
