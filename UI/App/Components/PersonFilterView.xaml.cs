using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Components;

/// <summary>
/// The self-contained person filter: header text, clear/toggle buttons, the fading fields panel, and
/// the filter state itself (a <see cref="PersonFilter"/> it owns), including the lazy marital-status/
/// year-bounds fetch. Host pages call <see cref="Initialize"/> right after their InitializeComponent,
/// subscribe to <see cref="Changed"/> to re-run their filter predicate, and call <see cref="Matches"/>
/// from it. The filter exists from construction, so <see cref="Matches"/> is safe even before
/// Initialize has run; only opening the panel requires it.
/// </summary>
public partial class PersonFilterView : ContentView
{
  private readonly PersonFilter _Filter = new();
  private ICancellationTokenProvider _CancellationTokenProvider = default!;
  private ICurrentProjectProvider _CurrentProjectProvider = default!;
  private IAlertService _AlertService = default!;
  private Func<Person[]> _SnapshotPersons = default!;
  private ICommand _FilterCommand = default!;
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
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _AlertService = alertService;
    _SnapshotPersons = snapshotPersons;
    // The XAML button bindings were evaluated in InitializeComponent, before the IAlertService the
    // command needs was available; notify so they re-resolve.
    _FilterCommand = new SafeCommand(OnFilterCommand, _AlertService);
    OnPropertyChanged(nameof(FilterCommand));

    // Label order must match PersonFilter's SexFilterValues/MaritalStatusFilterValues (index 0 = "any").
    // Assigning ItemsSource fires SelectedIndexChanged, so it runs under the same guard as any other
    // programmatic control write.
    _Syncing = true;
    SexFilterPicker.ItemsSource = new[]
    {
      UIStrings.FieldFilterAny,
      biologicalSexFormatter.ToString(BiologicalSex.Male),
      biologicalSexFormatter.ToString(BiologicalSex.Female),
      biologicalSexFormatter.ToString(BiologicalSex.Unknown),
    };
    MaritalStatusFilterPicker.ItemsSource = new[]
    {
      UIStrings.FieldFilterAny,
      UIStrings.FieldMaritalStatusMarried,
      UIStrings.FieldMaritalStatusSingle,
    };
    _Syncing = false;

    SyncControls();
  }

  /// <summary>Raised whenever a filter criterion changes (or lazily-fetched filter data lands), so
  /// the host page can re-run its filter predicate.</summary>
  public event EventHandler? Changed;

  /// <summary>Raised after the lazy marital-status/year-bounds fetch has been applied, on the main
  /// thread. The load-completion signal.</summary>
  public event EventHandler? FilterDataLoaded;

  public ICommand FilterCommand => _FilterCommand;

  public bool Matches(PersonInfo person) => _Filter.Matches(person);

  public bool IsAnyFilterActive => _Filter.IsAnyFilterActive;

  /// <summary>Re-fetches marital status/year bounds, e.g. after the page navigates to a different
  /// person/family whose person set has changed: immediately if the panel is currently open,
  /// otherwise on its next open. An immediate re-fetch snapshots the persons right away, so call
  /// this after the page's new person set has landed, not when the navigation merely starts.</summary>
  public void ResetFilterData()
  {
    _FilterDataLoaded = false;

    if (_IsFiltersVisible)
    {
      EnsureFilterDataLoaded();
    }
  }

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

  private void OnFilterCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "ToggleFiltersCommand":
        IsFiltersVisible = !IsFiltersVisible;
        break;
      case string commandName when commandName == "ClearFiltersCommand":
        _Filter.Clear();
        SyncControls();
        RaiseChanged();
        break;
    }
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

  /// <summary>Writes every control's value from the filter state; the single programmatic writer.</summary>
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

  // Marital status needs a relatives lookup no other part of a page's data requires, so it is fetched
  // lazily -- only once the filter panel is opened -- reusing the page's already-loaded persons
  // (snapshotted synchronously before handing off to the background fetch).
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
        // Maximum first: ComputeYearBounds floors maxYear at the current year, which is >= the
        // slider's initial Maximum of 1, so this order never leaves Minimum > Maximum mid-update.
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
