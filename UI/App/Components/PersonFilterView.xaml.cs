using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Components;

/// <summary>
/// The self-contained person filter: the active-criteria summary, the clear button, the fading
/// fields panel, and the filter state itself (a <see cref="PersonFilter"/> it owns), including the
/// lazy marital-status/year-bounds fetch. Each field control binds two-way to a criterion property
/// here, so the filter is the only place a criterion is written. It renders no toggle button of its
/// own: the host page carries one in its PageLayout menu, bound to <see cref="FilterCommand"/>
/// with "ToggleFiltersCommand".
/// Host pages call <see cref="Initialize"/> right after their InitializeComponent,
/// subscribe to <see cref="Changed"/> to re-run their filter predicate, and call <see cref="Matches"/>
/// from it. The filter exists from construction, so <see cref="Matches"/> is safe even before
/// Initialize has run; only opening the panel requires it.
/// </summary>
public partial class PersonFilterView : ContentView
{
  private static readonly TimeSpan InputSettleDelay = TimeSpan.FromSeconds(1);

  private readonly PersonFilter _Filter = new();
  private IDispatcherTimer? _InputSettleTimer;
  private ICancellationTokenProvider _CancellationTokenProvider = default!;
  private ICurrentProjectProvider _CurrentProjectProvider = default!;
  private IAlertService _AlertService = default!;
  private Func<Person[]> _SnapshotPersons = default!;
  private ICommand _FilterCommand = default!;
  private string[] _SexFilterLabels = [];
  private string[] _MaritalStatusFilterLabels = [];
  private bool _FilterDataLoaded;
  private bool _IsFiltersVisible;

  public PersonFilterView()
  {
    InitializeComponent();
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

    // Label order must match PersonFilter's SexFilterValues/MaritalStatusFilterValues (index 0 = "any").
    _SexFilterLabels =
    [
      UIStrings.FieldFilterAny,
      biologicalSexFormatter.ToString(BiologicalSex.Male),
      biologicalSexFormatter.ToString(BiologicalSex.Female),
      biologicalSexFormatter.ToString(BiologicalSex.Unknown),
    ];
    _MaritalStatusFilterLabels =
    [
      UIStrings.FieldFilterAny,
      UIStrings.FieldMaritalStatusMarried,
      UIStrings.FieldMaritalStatusSingle,
    ];
    this.RefreshView();
  }

  /// <summary>Raised whenever a filter criterion changes (or lazily-fetched filter data lands), so
  /// the host page can re-run its filter predicate. The two continuously-edited criteria -- the name
  /// text and the year -- coalesce: each edit restarts a delay and only the settled value is raised,
  /// so a host never repopulates its collection for an intermediate keystroke or slider position.
  /// Every other criterion raises immediately.</summary>
  public event EventHandler? Changed;

  /// <summary>Raised after the lazy marital-status/year-bounds fetch has been applied, on the main
  /// thread. The load-completion signal.</summary>
  public event EventHandler? FilterDataLoaded;

  public ICommand FilterCommand => _FilterCommand;

  public bool Matches(PersonInfo person) => _Filter.Matches(person);

  public bool IsAnyFilterActive => _Filter.IsAnyFilterActive;

  /// <summary>Non-empty exactly when <see cref="IsAnyFilterActive"/> is true: every criterion that
  /// one counts contributes a part.</summary>
  public string CurrentFilterSet
  {
    get
    {
      var ret = new List<string>();

      if (!string.IsNullOrEmpty(_Filter.NameFilter))
      {
        ret.Add(string.Format(UIStrings.PersonFilterPart_2, UIStrings.FieldSearchText, _Filter.NameFilter));
      }

      if (_Filter.SexFilterIndex > 0)
      {
        var sex = _SexFilterLabels[_Filter.SexFilterIndex];
        ret.Add(string.Format(UIStrings.PersonFilterPart_2, UIStrings.FieldFilterSex, sex));
      }

      if (_Filter.MaritalStatusFilterIndex > 0)
      {
        var maritalStatus = _MaritalStatusFilterLabels[_Filter.MaritalStatusFilterIndex];
        ret.Add(string.Format(UIStrings.PersonFilterPart_2, UIStrings.FieldMaritalStatus, maritalStatus));
      }

      if (_Filter.IsYearFilterEnabled)
      {
        ret.Add(string.Format(UIStrings.PersonFilterPart_2, UIStrings.FieldYear, SelectedYearText));
      }

      return string.Join("; ", ret);
    }
  }

  public string[] SexFilterLabels => _SexFilterLabels;

  public string[] MaritalStatusFilterLabels => _MaritalStatusFilterLabels;

  public string NameFilter
  {
    get => _Filter.NameFilter;
    set
    {
      if (_Filter.NameFilter == value)
      {
        return;
      }

      _Filter.NameFilter = value;
      RaiseChangedWhenInputSettles();
    }
  }

  public int SexFilterIndex
  {
    get => _Filter.SexFilterIndex;
    set
    {
      if (_Filter.SexFilterIndex == value)
      {
        return;
      }

      _Filter.SexFilterIndex = value;
      RaiseChanged();
    }
  }

  public int MaritalStatusFilterIndex
  {
    get => _Filter.MaritalStatusFilterIndex;
    set
    {
      if (_Filter.MaritalStatusFilterIndex == value)
      {
        return;
      }

      _Filter.MaritalStatusFilterIndex = value;
      RaiseChanged();
    }
  }

  public bool IsYearFilterEnabled
  {
    get => _Filter.IsYearFilterEnabled;
    set
    {
      if (_Filter.IsYearFilterEnabled == value)
      {
        return;
      }

      _Filter.IsYearFilterEnabled = value;

      RaiseChanged();
    }
  }

  public double MaxYear => _Filter.MaxYear;

  public double MinYear => _Filter.MinYear;

  public double SelectedYear
  {
    get => _Filter.SelectedYear;
    set
    {
      // The filter floors the value, so most of a drag's fractional updates are no-ops.
      var previousYear = _Filter.SelectedYear;
      _Filter.SelectedYear = value;
      if (_Filter.SelectedYear == previousYear)
      {
        return;
      }

      RaiseChangedWhenInputSettles();
    }
  }

  public string SelectedYearText => ((int)_Filter.SelectedYear).ToString();

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
      if (_IsFiltersVisible == value)
      {
        return;
      }

      _IsFiltersVisible = value;
      this.RefreshView();

      if (_IsFiltersVisible)
      {
        EnsureFilterDataLoaded();
      }
    }
  }

  private void OnFilterCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "ToggleFiltersCommand":
        IsFiltersVisible = !IsFiltersVisible;
        break;
      case string commandName when commandName == "ClearFiltersCommand":
        _Filter.Clear();
        RaiseChanged();
        break;
    }
  }

  // The criterion is already written, so Matches, the summary and the year readout stay live while
  // the user is still typing or dragging; only the host page's re-filter waits for the input to
  // settle. A settled value that arrives before the delay elapses (a picker, the clear button, the
  // bounds fetch) supersedes the pending pass rather than queueing behind it.
  private void RaiseChangedWhenInputSettles()
  {
    this.RefreshView();

    var timer = GetInputSettleTimer();
    timer.Stop();
    timer.Start();
  }

  private void RaiseChanged()
  {
    // Cancel after RefreshView, not before: pushing widened year bounds makes the slider clamp its
    // own Value back through the binding, which arms the timer from inside RefreshView itself.
    this.RefreshView();
    _InputSettleTimer?.Stop();
    Changed?.Invoke(this, EventArgs.Empty);
  }

  private IDispatcherTimer GetInputSettleTimer()
  {
    if (_InputSettleTimer is not null)
    {
      return _InputSettleTimer;
    }

    var timer = Dispatcher.CreateTimer();
    timer.Interval = InputSettleDelay;
    timer.IsRepeating = false;
    timer.Tick += (_, _) => RaiseChanged();
    _InputSettleTimer = timer;
    return timer;
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
        var selectedYear = _Filter.SelectedYear;

        this.RefreshView();
        // Pushing the widened bounds makes the slider clamp its own Value up off zero and carry that
        // clamp back through the binding, so the year the bounds picked is restored after them.
        _Filter.SelectedYear = selectedYear;
        RaiseChanged();
        FilterDataLoaded?.Invoke(this, EventArgs.Empty);
      }, _AlertService);
    }

    SafeTask.Run(LoadFilterDataAsync, _AlertService);
  }
}
