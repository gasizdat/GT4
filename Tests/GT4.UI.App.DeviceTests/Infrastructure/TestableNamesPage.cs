using GT4.Core.Project.Dto;
using GT4.UI.Pages;
using System.ComponentModel;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes NamesPage's protected seams for testing. Awaiting OnDeleteCommandAsync directly bypasses
/// SafeCommand's error routing (which needs Shell.Current, absent in this host); RequestUpdateNames
/// is the only way to set the getter-only CurrentName selection.
/// </summary>
internal sealed class TestableNamesPage(IServiceProvider services) : NamesPage(services)
{
  public Task InvokeDeleteAsync(object parameter) => OnDeleteCommandAsync(parameter);

  public Task InvokeAddNameAsync() => OnAddName();

  public Task InvokeEditAsync(Name name) => OnEditCommandAsync(name);

  public void InvokeRequestUpdateNames(Name? selected = null) => RequestUpdateNames(selected);

  /// <summary>
  /// Runs <paramref name="interact"/> on the UI thread, triggers a Names reload, and waits for the
  /// reload to complete (signalled by the page's own PropertyChanged(CurrentName) at the end of its
  /// background-load pipeline), then returns the reloaded items.
  /// </summary>
  public async Task<Name[]> ReloadNamesAsync(Action? interact = null, TimeSpan? timeout = null)
  {
    var loadCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName == nameof(NamesPage.CurrentName))
      {
        loadCompleted.TrySetResult();
      }
    }

    PropertyChanged += OnPropertyChanged;
    try
    {
      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        interact?.Invoke();
        _ = Names;
      });

      var completed = await Task.WhenAny(loadCompleted.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(10)));
      if (completed != loadCompleted.Task)
      {
        throw new TimeoutException("Names reload did not complete; check the TestServices mock setup.");
      }

      return await MainThread.InvokeOnMainThreadAsync(() => Names.ToArray());
    }
    finally
    {
      PropertyChanged -= OnPropertyChanged;
    }
  }

}
