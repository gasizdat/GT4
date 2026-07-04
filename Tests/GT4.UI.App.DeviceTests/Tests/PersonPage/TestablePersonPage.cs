using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Pages;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using System.Runtime.CompilerServices;

namespace GT4.UI.DeviceTests;

/// <summary>
/// PersonPage loads through a fire-and-forget Task.Run (ShowPersonInfo/the PersonInfo setter), with
/// no Task a test can await. CompletedLoads counts UpdateUI's own RefreshView() firing
/// OnPropertyChanged(FullName) -- mirrors TestableNamesPage's CompletedLoads for the same reason.
/// </summary>
internal sealed class TestablePersonPage : PersonPage
{
  private int _CompletedLoads;

  public TestablePersonPage(
    IServiceProvider serviceProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IDateSpanFormatter dateSpanFormatter,
    IDateFormatter dateFormatter,
    INameFormatter nameFormatter,
    [FromKeyedServices(DataCategory.PersonBio)]
    IDataConverter textConverter,
    [FromKeyedServices(DataCategory.PersonGedcomTags)]
    IDataConverter gedcomConverter,
    IAlertService alertService,
    INavigationService navigationService)
    : base(
      serviceProvider,
      cancellationTokenProvider,
      currentProjectProvider,
      dateSpanFormatter,
      dateFormatter,
      nameFormatter,
      textConverter,
      gedcomConverter,
      alertService,
      navigationService)
  {
  }

  public int CompletedLoads => _CompletedLoads;

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == nameof(FullName))
    {
      _CompletedLoads++;
    }
  }
}
