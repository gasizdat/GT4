using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Runtime.CompilerServices;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes DateCalendarPage's load completion for testing, same level-triggered-counter approach
/// as TestableStatisticsPage.CompletedLoads: counts each time RefreshView's OnPropertyChanged sweep
/// touches CalendarMonth, since the lazy load has no other observable completion signal.
/// </summary>
internal sealed class TestableDateCalendarPage : DateCalendarPage
{
  private int _CompletedLoads;

  public TestableDateCalendarPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter)
    : base(currentProjectProvider, cancellationTokenProvider, alertService, nameFormatter)
  {
  }

  public int CompletedLoads => _CompletedLoads;

  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == nameof(CalendarMonth))
    {
      _CompletedLoads++;
    }
  }

  // NavigatedToEventArgs has no accessible test-side constructor and OnNavigatedTo never reads it.
  public void InvokeNavigatedTo() => OnNavigatedTo(this, null!);

  public async Task WaitForFirstLoadAsync(TimeSpan? timeout = null)
  {
    await Poll.UntilAsync(
      () => Task.FromResult(_CompletedLoads),
      loads => loads >= 1,
      timeout ?? TimeSpan.FromSeconds(10),
      "CalendarMonth did not finish loading; check the TestServices mock setup.");
  }

  public async Task ReloadAsync(Action interact, TimeSpan? timeout = null)
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
      "CalendarMonth reload did not complete; check the TestServices mock setup.");
  }
}
