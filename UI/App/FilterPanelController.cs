using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GT4.UI;

/// <summary>
/// Shared filters-panel scaffolding reused by every page that hosts a <see cref="PersonInfoFilter"/>
/// (ProjectPage, FamilyPage, PersonPage): panel visibility, the toggle button label, and the lazy
/// marital-status/year-bounds fetch gate. Each page still owns its own person snapshot -- families
/// flattened, a member list, or a relatives roots array all have a different shape -- so that part is
/// supplied as a delegate rather than pulled in here.
/// </summary>
internal sealed class FilterPanelController : INotifyPropertyChanged
{
  private readonly PersonInfoFilter _Filter;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IAlertService _AlertService;
  private readonly Func<Person[]> _SnapshotPersons;
  private bool _FilterDataLoaded;
  private bool _IsFiltersVisible;

  public FilterPanelController(
    PersonInfoFilter filter,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IAlertService alertService,
    Func<Person[]> snapshotPersons)
  {
    _Filter = filter;
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _AlertService = alertService;
    _SnapshotPersons = snapshotPersons;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public bool IsAnyFilterActive => _Filter.IsAnyFilterActive;

  public bool IsFiltersVisible
  {
    get => _IsFiltersVisible;
    set
    {
      _IsFiltersVisible = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ToggleFiltersButtonName));

      if (value)
      {
        EnsureFilterDataLoaded();
      }
    }
  }

  public string ToggleFiltersButtonName =>
    string.Format(UIStrings.BtnNameFilters_1, IsFiltersVisible ? "🔼" : "🔽");

  public void ClearFilters() => _Filter.Clear();

  /// <summary>Forces the next filter-panel open to re-fetch marital status/year bounds, e.g. after the
  /// page navigates to a different person/family whose person set has changed.</summary>
  public void ResetFilterData() => _FilterDataLoaded = false;

  // Marital status needs a relatives lookup that no other part of a page's data requires, so it is
  // fetched lazily -- only once the filter panel is actually opened -- reusing the persons the page
  // already has loaded (snapshotted via _SnapshotPersons, on the main thread, before handing off to
  // the background fetch) rather than fetching it unconditionally for every visit to the page.
  private void EnsureFilterDataLoaded()
  {
    if (_FilterDataLoaded)
    {
      return;
    }
    _FilterDataLoaded = true;

    var persons = _SnapshotPersons();

    async Task LoadFilterDataAsync()
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var (marriedIds, minYear, maxYear) = await PersonInfoFilter.FetchMarriedAndYearBoundsAsync(
        persons, _CurrentProjectProvider.Project.Relatives, token);

      await SafeTask.RunOnMainThread(() =>
      {
        _Filter.SetMarriedIds(marriedIds);
        _Filter.SetYearBounds(minYear, maxYear);
      }, _AlertService);
    }

    SafeTask.Run(LoadFilterDataAsync, _AlertService);
  }
}
