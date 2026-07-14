using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Pages;
using GT4.UI.Utils;
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
  private int _FilterDataLoads;

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
    INavigationService navigationService,
    IBiologicalSexFormatter biologicalSexFormatter)
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
      navigationService,
      biologicalSexFormatter)
  {
    // FilterDataLoaded fires exactly once per lazy fetch, after SetMarriedIds/SetYearBounds have
    // been applied on the main thread.
    FilterView = this.FindByName<PersonFilterView>("FilterView");
    FilterView.FilterDataLoaded += (_, _) => _FilterDataLoads++;
  }

  public PersonFilterView FilterView { get; }

  public int CompletedLoads => _CompletedLoads;

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  public void ForceSizeAllocated(double width, double height) => OnSizeAllocated(width, height);

  public ScrollView BodyScrollForTest => BodyScroll;

  public ImagePresenter PersonPhotoForTest => PersonPhotoView;

  public CollectionView RelativesListForTest => RelativesListView;

  /// <summary>
  /// Opens the filter panel (if not already open) and waits for the resulting lazy marital-status
  /// fetch + year-bounds computation to land on the filter.
  /// </summary>
  public async Task WaitForFilterDataAsync(TimeSpan? timeout = null)
  {
    var loadsBefore = _FilterDataLoads;
    await MainThread.InvokeOnMainThreadAsync(() => FilterView.IsFiltersVisible = true);

    await Poll.UntilAsync(
      () => Task.FromResult(_FilterDataLoads),
      loads => loads > loadsBefore,
      timeout ?? TimeSpan.FromSeconds(10),
      "Filter data did not finish loading; check the TestServices mock setup.");
  }

  protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == nameof(FullName))
    {
      _CompletedLoads++;
    }
  }
}
