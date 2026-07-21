using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Runtime.CompilerServices;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes StatisticsPage's load completion for testing. The Statistics getter's lazy load (mirroring
/// NamesPage.Names) has no other observable completion signal, so this counts each time RefreshView's
/// OnPropertyChanged sweep touches Statistics -- the same level-triggered-counter approach as
/// TestableNamesPage.CompletedLoads, needed because a load may finish before a test starts waiting on it.
/// </summary>
internal sealed class TestableStatisticsPage : StatisticsPage
{
  private int _CompletedLoads;

  public TestableStatisticsPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter,
    IProjectRevisionMonitor projectRevisionMonitor)
    : base(currentProjectProvider, cancellationTokenProvider, alertService, nameFormatter, projectRevisionMonitor)
  {
  }

  public int CompletedLoads => _CompletedLoads;

  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == nameof(Statistics))
    {
      _CompletedLoads++;
    }
  }

  // NavigatedToEventArgs has no accessible test-side constructor and OnNavigatedTo never reads it.
  public void InvokeNavigatedTo() => OnNavigatedTo(this, null!);

  /// <summary>
  /// Waits for the page's own automatic first load to finish, then returns the loaded statistics.
  /// Safe even if that load already finished before the caller started waiting (CompletedLoads is
  /// level-triggered), which matters here: XAML evaluates the page's bound *Text properties (each of
  /// which reads Statistics) as soon as InitializeComponent runs, so the first load may already be
  /// in flight -- or done -- before a test can react to page construction.
  /// </summary>
  public async Task<ProjectStatistics> WaitForFirstLoadAsync(TimeSpan? timeout = null)
  {
    await Poll.UntilAsync(
      () => Task.FromResult(_CompletedLoads),
      loads => loads >= 1,
      timeout ?? TimeSpan.FromSeconds(10),
      "Statistics did not finish loading; check the TestServices mock setup.");

    return await MainThread.InvokeOnMainThreadAsync(() => Statistics);
  }

  /// <summary>
  /// Triggers a (re)load and waits for a completion after that point (CompletedLoads is snapshotted
  /// on the UI thread right before <paramref name="interact"/>, so an earlier completion can't satisfy
  /// the wait), then returns the reloaded statistics. Only use this once the first load has already
  /// settled (see WaitForFirstLoadAsync) and <paramref name="interact"/> is guaranteed to cause a new
  /// one (e.g. a genuine revision change) -- otherwise a no-op reload request would hang until timeout.
  /// </summary>
  public async Task<ProjectStatistics> ReloadStatisticsAsync(Action interact, TimeSpan? timeout = null)
  {
    var loadsBefore = 0;

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      loadsBefore = _CompletedLoads;
      interact();
    });

    await Poll.UntilAsync(
      () => Task.FromResult(_CompletedLoads),
      loads => loads > loadsBefore,
      timeout ?? TimeSpan.FromSeconds(10),
      "Statistics reload did not complete; check the TestServices mock setup.");

    return await MainThread.InvokeOnMainThreadAsync(() => Statistics);
  }
}
