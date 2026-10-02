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
/// FamilyTreePage loads through a fire-and-forget SafeTask.Run (Reload, triggered by the PersonInfo
/// setter/OnPageCommand). CompletedLoads counts LoadInProgress ticking back to false (the load's own
/// finally-block signal), same shape as TestableNamesPage/TestablePersonPage's counters.
/// </summary>
internal sealed class TestableFamilyTreePage : FamilyTreePage
{
  private int _CompletedLoads;

  public TestableFamilyTreePage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IFamilyTreeArrangementStore arrangementStore,
    INameFormatter nameFormatter,
    FontScale? fontScale,
    IAlertService alertService,
    INavigationService navigationService,
    DataConverterResolver dataConverterResolver)
    : base(
      cancellationTokenProvider,
      currentProjectProvider,
      arrangementStore,
      nameFormatter,
      fontScale,
      alertService,
      navigationService,
      dataConverterResolver)
  {
  }

  public int CompletedLoads => _CompletedLoads;

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  // The drag gesture itself is native-only; this is where it lands.
  public void InvokeDropNode(int personId, double deltaX) => DropNode(personId, deltaX);

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
