using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Components;

/// <summary>
/// The self-contained person filter: header text, clear/toggle buttons, the fading fields panel, and
/// the filter state itself (a <see cref="PersonFilter"/> it owns), including the lazy marital-status/
/// year-bounds fetch. Host pages call <see cref="Initialize"/> right after their InitializeComponent,
/// subscribe to <see cref="Changed"/> to re-run their filter predicate, and call <see cref="Matches"/>
/// from it; nothing may touch the filter before Initialize runs.
/// </summary>
public partial class PersonFilterView : ContentView
{
  private PersonFilter _Filter = default!;
  private ICancellationTokenProvider _CancellationTokenProvider = default!;
  private ICurrentProjectProvider _CurrentProjectProvider = default!;
  private IAlertService _AlertService = default!;
  private Func<Person[]> _SnapshotPersons = default!;
  private bool _FilterDataLoaded;
  private bool _IsFiltersVisible;
  private bool _Syncing;

  public PersonFilterView()
  {
    InitializeComponent();
    UpdateToggleText();
  }

  public static readonly BindableProperty HeaderTextProperty = BindableProperty.Create(
    nameof(HeaderText),
    typeof(string),
    typeof(PersonFilterView),
    default(string),
    BindingMode.OneWay);

  public string? HeaderText
  {
    get => (string?)GetValue(HeaderTextProperty);
    set => SetValue(HeaderTextProperty, value);
  }

  public void Initialize(
    IBiologicalSexFormatter biologicalSexFormatter,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IAlertService alertService,
    Func<Person[]> snapshotPersons)
  {
    _Filter = new PersonFilter(biologicalSexFormatter);
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _AlertService = alertService;
    _SnapshotPersons = snapshotPersons;

    // Assigning ItemsSource fires SelectedIndexChanged (with platform-dependent index
    // normalization), so it runs under the same guard as any other programmatic control write.
    _Syncing = true;
    SexFilterPicker.ItemsSource = _Filter.SexFilterLabels;
    MaritalStatusFilterPicker.ItemsSource = _Filter.MaritalStatusFilterLabels;
    _Syncing = false;

    SyncControls();
  }

  /// <summary>Raised whenever a filter criterion changes (or lazily-fetched filter data lands), so
  /// the host page can re-run its filter predicate.</summary>
  public event EventHandler? Changed;

  /// <summary>Raised after the lazy marital-status/year-bounds fetch has been applied, on the main
  /// thread. The load-completion signal.</summary>
  public event EventHandler? FilterDataLoaded;

  public bool Matches(PersonInfo person) => _Filter.Matches(person);

  public bool IsAnyFilterActive => _Filter.IsAnyFilterActive;

  /// <summary>Forces the next filter-panel open to re-fetch marital status/year bounds, e.g. after
  /// the page navigates to a different person/family whose person set has changed.</summary>
  public void ResetFilterData() => _FilterDataLoaded = false;

  public bool IsFiltersVisible
  {
    get => _IsFiltersVisible;
    set
    {
      _IsFiltersVisible = value;
      FiltersPanelFade.IsVisible = value;
      UpdateToggleText();

      if (value)
      {
        EnsureFilterDataLoaded();
      }
    }
  }

  private void UpdateToggleText() =>
    ToggleFiltersButton.Text = string.Format(UIStrings.BtnNameFilters_1, _IsFiltersVisible ? "🔼" : "🔽");

  private void OnToggleFiltersClicked(object? sender, EventArgs e) => IsFiltersVisible = !IsFiltersVisible;

  private void OnClearFiltersClicked(object? sender, EventArgs e)
  {
    _Filter.Clear();
    SyncControls();
    RaiseChanged();
  }

  private void OnNameFilterChanged(object? sender, TextChangedEventArgs e)
  {
    if (_Syncing)
    {
      return;
    }
    _Filter.NameFilter = e.NewTextValue;
    RaiseChanged();
  }

  private void OnSexFilterChanged(object? sender, EventArgs e)
  {
    if (_Syncing)
    {
      return;
    }
    _Filter.SexFilterIndex = SexFilterPicker.SelectedIndex;
    RaiseChanged();
  }

  private void OnMaritalStatusFilterChanged(object? sender, EventArgs e)
  {
    if (_Syncing)
    {
      return;
    }
    _Filter.MaritalStatusFilterIndex = MaritalStatusFilterPicker.SelectedIndex;
    RaiseChanged();
  }

  private void OnYearFilterToggled(object? sender, ToggledEventArgs e)
  {
    if (_Syncing)
    {
      return;
    }
    _Filter.IsYearFilterEnabled = e.Value;
    YearSlider.IsEnabled = e.Value;
    RaiseChanged();
  }

  private void OnYearSliderChanged(object? sender, ValueChangedEventArgs e)
  {
    if (_Syncing)
    {
      return;
    }

    // The filter floors the value, so most of a drag's fractional updates are no-ops.
    var previousYear = _Filter.SelectedYear;
    _Filter.SelectedYear = e.NewValue;
    if (_Filter.SelectedYear != previousYear)
    {
      SelectedYearLabel.Text = ((int)_Filter.SelectedYear).ToString();
      RaiseChanged();
    }
  }

  /// <summary>Writes every control's value from the filter state. The single programmatic writer:
  /// a missed control here would silently desync the UI from the filter.</summary>
  private void SyncControls()
  {
    _Syncing = true;
    NameFilterEntry.Text = _Filter.NameFilter;
    SexFilterPicker.SelectedIndex = _Filter.SexFilterIndex;
    MaritalStatusFilterPicker.SelectedIndex = _Filter.MaritalStatusFilterIndex;
    YearFilterSwitch.IsToggled = _Filter.IsYearFilterEnabled;
    YearSlider.IsEnabled = _Filter.IsYearFilterEnabled;
    YearSlider.Value = _Filter.SelectedYear;
    SelectedYearLabel.Text = ((int)_Filter.SelectedYear).ToString();
    _Syncing = false;
  }

  private void RaiseChanged()
  {
    ClearButtonFade.IsVisible = _Filter.IsAnyFilterActive;
    Changed?.Invoke(this, EventArgs.Empty);
  }

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
      var (marriedIds, minYear, maxYear) = await PersonFilter.FetchMarriedAndYearBoundsAsync(
        persons, _CurrentProjectProvider.Project.Relatives, token);

      await SafeTask.RunOnMainThread(() =>
      {
        _Filter.SetMarriedIds(marriedIds);
        _Filter.SetYearBounds(minYear, maxYear);

        _Syncing = true;
        // Maximum first: the slider starts at [0, 1] and the new maximum is always >= the current
        // year, so this order never leaves Minimum > Maximum mid-update.
        YearSlider.Maximum = maxYear;
        YearSlider.Minimum = minYear;
        YearSlider.Value = _Filter.SelectedYear;
        SelectedYearLabel.Text = ((int)_Filter.SelectedYear).ToString();
        _Syncing = false;

        RaiseChanged();
        FilterDataLoaded?.Invoke(this, EventArgs.Empty);
      }, _AlertService);
    }

    SafeTask.Run(LoadFilterDataAsync, _AlertService);
  }
}
