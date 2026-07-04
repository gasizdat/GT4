using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Pages;
using GT4.UI.Utils.Formatters;
using System.Runtime.CompilerServices;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes NamesPage's protected seams for testing. Awaiting OnDeleteCommandAsync directly bypasses
/// SafeCommand's error routing (which needs Shell.Current, absent in this host); RequestUpdateNames
/// is the only way to set the getter-only CurrentName selection.
/// </summary>
internal sealed class TestableNamesPage : NamesPage
{
  private int _CompletedLoads;

  public TestableNamesPage(IServiceProvider services) : base(
    services.GetRequiredService<ICurrentProjectProvider>(),
    services.GetRequiredService<ICancellationTokenProvider>(),
    services.GetRequiredService<IComparer<Name>>(),
    services.GetRequiredService<INameTypeFormatter>(),
    services.GetRequiredService<IBiologicalSexFormatter>(),
    services.GetRequiredService<INameFormatter>(),
    services.GetRequiredService<IPageAlertService>())
  {
  }

  public Task InvokeDeleteAsync(object parameter) => OnDeleteCommandAsync(parameter);

  public Task InvokeAddNameAsync() => OnAddName();

  public Task InvokeEditAsync(Name name) => OnEditCommandAsync(name);

  public void InvokeRequestUpdateNames(Name? selected = null) => RequestUpdateNames(selected);

  /// <summary>
  /// How many background Names loads have finished — each one ends with the page's own
  /// OnPropertyChanged(CurrentName) on the UI thread. A level-triggered counter, unlike a
  /// PropertyChanged subscription, can't miss a completion that lands before the caller starts
  /// waiting (see Poll's doc for the race).
  /// </summary>
  public int CompletedLoads => _CompletedLoads;

  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == nameof(CurrentName))
    {
      _CompletedLoads++;
    }
  }

  /// <summary>
  /// Runs <paramref name="interact"/> on the UI thread, triggers a Names reload, waits for a load
  /// completion after that point (CompletedLoads is snapshotted on the UI thread right before
  /// <paramref name="interact"/>, so an earlier completion can't satisfy the wait), then returns
  /// the reloaded items.
  /// </summary>
  public async Task<Name[]> ReloadNamesAsync(Action interact, TimeSpan? timeout = null)
  {
    var loadsBefore = 0;

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      loadsBefore = _CompletedLoads;
      interact();
      _ = Names;
    });

    await Poll.UntilAsync(
      () => Task.FromResult(_CompletedLoads),
      loads => loads > loadsBefore,
      timeout ?? TimeSpan.FromSeconds(10),
      "Names reload did not complete; check the TestServices mock setup.");

    return await MainThread.InvokeOnMainThreadAsync(() => Names.ToArray());
  }
}
