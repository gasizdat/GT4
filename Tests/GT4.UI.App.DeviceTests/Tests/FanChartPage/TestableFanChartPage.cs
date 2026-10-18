using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using System.Runtime.CompilerServices;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Same load counter as TestableFamilyTreePage: CompletedLoads counts LoadInProgress ticking back to
/// false at the end of each fire-and-forget load.
/// </summary>
internal sealed class TestableFanChartPage : FanChartPage
{
  private int _CompletedLoads;

  public TestableFanChartPage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IFamilyTreeHiddenPersonsStore hiddenPersonsStore,
    INameFormatter nameFormatter,
    ILifeDatesFormatter lifeDatesFormatter,
    ISettingEditor sweepSetting,
    ISettingEditor calendarSetting,
    FontScale? fontScale,
    IAlertService alertService,
    INavigationService navigationService,
    DataConverterResolver dataConverterResolver)
    : base(
      cancellationTokenProvider,
      currentProjectProvider,
      hiddenPersonsStore,
      nameFormatter,
      lifeDatesFormatter,
      sweepSetting,
      calendarSetting,
      fontScale,
      alertService,
      navigationService,
      dataConverterResolver)
  {
  }

  public int CompletedLoads => _CompletedLoads;

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  // The tap gesture itself is native-only; this is where it lands.
  public void InvokeTapAt(Point point) => TapAt(point);

  // Likewise the pointer's moves and exit, which passes null.
  public void InvokeHoverAt(Point? point) => HoverAt(point);

  // NavigatedToEventArgs has no accessible test-side constructor and OnNavigatedTo never reads it.
  public void InvokeNavigatedTo() => OnNavigatedTo(null!);

  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == nameof(LoadInProgress) && !LoadInProgress)
    {
      _CompletedLoads++;
    }
  }
}
