using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GT4.UI.Utils;

/// <summary>
/// Shared name/sex/marital-status/alive-in-year filter state and matching, reused by every page that
/// shows a filterable list of persons (ProjectPage's families, FamilyPage's members, PersonPage's
/// relatives). Bind a <see cref="PersonFilterFieldsView"/> to an instance for the field UI; call
/// <see cref="Matches"/> from the page's own <c>FilteredObservableCollection{T}</c> predicate.
/// Marital status requires a relatives lookup a page doesn't otherwise need, so it isn't part of the
/// person data a page loads up front -- pages call <see cref="SetMarriedIds"/> lazily (e.g. the first
/// time the filter panel is shown) once they've fetched it, using whatever person list they already
/// have loaded.
/// </summary>
public sealed class PersonInfoFilter : INotifyPropertyChanged
{
  private static readonly BiologicalSex?[] SexFilterValues = [null, BiologicalSex.Male, BiologicalSex.Female, BiologicalSex.Unknown];
  private static readonly bool?[] MaritalStatusFilterValues = [null, true, false];

  private string _NameFilter = string.Empty;
  private int _SexFilterIndex;
  private int _MaritalStatusFilterIndex;
  private bool _IsYearFilterEnabled;
  private double _SelectedYear;
  private double _MinYear;
  private double _MaxYear;
  private HashSet<int> _MarriedIds = [];

  public PersonInfoFilter(IBiologicalSexFormatter biologicalSexFormatter)
  {
    SexFilterLabels =
    [
      UIStrings.FieldFilterAny,
      biologicalSexFormatter.ToString(BiologicalSex.Male),
      biologicalSexFormatter.ToString(BiologicalSex.Female),
      biologicalSexFormatter.ToString(BiologicalSex.Unknown),
    ];
    MaritalStatusFilterLabels =
    [
      UIStrings.FieldFilterAny,
      UIStrings.FieldMaritalStatusMarried,
      UIStrings.FieldMaritalStatusSingle,
    ];
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  /// <summary>Raised whenever a filter criterion changes, so a page can re-run its filter predicate.</summary>
  public event EventHandler? Changed;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public string[] SexFilterLabels { get; }

  public string[] MaritalStatusFilterLabels { get; }

  public string NameFilter
  {
    get => _NameFilter;
    set
    {
      _NameFilter = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsAnyFilterActive));
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  public int SexFilterIndex
  {
    get => _SexFilterIndex;
    set
    {
      _SexFilterIndex = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsAnyFilterActive));
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  public int MaritalStatusFilterIndex
  {
    get => _MaritalStatusFilterIndex;
    set
    {
      _MaritalStatusFilterIndex = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsAnyFilterActive));
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  public bool IsYearFilterEnabled
  {
    get => _IsYearFilterEnabled;
    set
    {
      _IsYearFilterEnabled = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsAnyFilterActive));
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  public double SelectedYear
  {
    get => _SelectedYear;
    set
    {
      _SelectedYear = value;
      OnPropertyChanged();
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }

  public double MinYear => _MinYear;

  public double MaxYear => _MaxYear;

  public bool IsAnyFilterActive =>
    !string.IsNullOrEmpty(_NameFilter) ||
    _SexFilterIndex != 0 ||
    _MaritalStatusFilterIndex != 0 ||
    _IsYearFilterEnabled;

  /// <summary>Recomputes the year slider's bounds (e.g. after a page (re)loads its person set),
  /// clamping SelectedYear back into range if it fell outside it.</summary>
  public void SetYearBounds(double min, double max)
  {
    _MinYear = min;
    _MaxYear = max;
    OnPropertyChanged(nameof(MinYear));
    OnPropertyChanged(nameof(MaxYear));

    if (_SelectedYear < min || _SelectedYear > max)
    {
      SelectedYear = max;
    }
  }

  /// <summary>Sets which persons (by Id) are married, e.g. from a lazily-fetched relatives lookup.
  /// Re-runs the page's filter predicate, same as any other criterion changing.</summary>
  public void SetMarriedIds(IEnumerable<int> marriedIds)
  {
    _MarriedIds = [.. marriedIds];
    Changed?.Invoke(this, EventArgs.Empty);
  }

  public void Clear()
  {
    _NameFilter = string.Empty;
    _SexFilterIndex = 0;
    _MaritalStatusFilterIndex = 0;
    _IsYearFilterEnabled = false;
    _SelectedYear = _MaxYear;

    OnPropertyChanged(nameof(NameFilter));
    OnPropertyChanged(nameof(SexFilterIndex));
    OnPropertyChanged(nameof(MaritalStatusFilterIndex));
    OnPropertyChanged(nameof(IsYearFilterEnabled));
    OnPropertyChanged(nameof(SelectedYear));
    OnPropertyChanged(nameof(IsAnyFilterActive));
    Changed?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>Computes the year-slider bounds from a person set's known birth/death years, falling
  /// back to a century before the current year when nothing is known. Takes the base Person type
  /// (rather than PersonInfo) since that's all it needs, so a page's own RelativeInfo[] roots pass
  /// straight through via array covariance instead of needing a PersonInfo projection.</summary>
  public static (double Min, double Max) ComputeYearBounds(IReadOnlyCollection<Person> persons)
  {
    const int FallbackYearsBack = 100;

    var knownYears = persons
      .SelectMany(p => new[]
      {
        p.BirthDate.Status == DateStatus.Unknown ? (int?)null : p.BirthDate.Year,
        p.DeathDate is { Status: not DateStatus.Unknown } d ? d.Year : (int?)null,
      })
      .Where(y => y.HasValue)
      .Select(y => y!.Value)
      .ToList();

    var currentYear = Date.Now.Year;
    var min = knownYears.Count > 0 ? knownYears.Min() : currentYear - FallbackYearsBack;
    var max = Math.Max(knownYears.Count > 0 ? knownYears.Max() : currentYear, currentYear);

    return (min, max);
  }

  /// <summary>Fetches relatives for the given (unfiltered) person set and derives the married-ids/
  /// year-bounds data a filter panel needs. Pure -- does not touch this filter's own state -- so it's
  /// safe to await from a background thread; callers apply the result via SetMarriedIds/SetYearBounds
  /// themselves, from whichever thread their own app-lifecycle-aware threading helpers (SafeTask, in
  /// GT4.UI.App) land on.</summary>
  public static async Task<(int[] MarriedIds, double MinYear, double MaxYear)> FetchMarriedAndYearBoundsAsync(
    Person[] persons, ITableRelatives relatives, CancellationToken token)
  {
    var relativesByPerson = await relatives.GetRelativesForPersonsAsync(persons, token);
    var marriedIds = relativesByPerson
      .Where(kv => kv.Value.Any(r => r.Type == RelationshipType.Spouse))
      .Select(kv => kv.Key)
      .ToArray();
    var (minYear, maxYear) = ComputeYearBounds(persons);

    return (marriedIds, minYear, maxYear);
  }

  public bool Matches(PersonInfo person)
  {
    // Guarded on _NameFilter being set (unlike relying on WildcardMatcher.IsMatch(x, "") == true):
    // a person with zero Names (e.g. a bare relative row with no name data) would otherwise never
    // pass Any(...) even with no filter active.
    if (!string.IsNullOrEmpty(_NameFilter) && !person.Names.Any(n => WildcardMatcher.IsMatch(n.Value, _NameFilter)))
    {
      return false;
    }

    if (SexFilterValues[_SexFilterIndex] is { } sex && person.BiologicalSex != sex)
    {
      return false;
    }

    if (MaritalStatusFilterValues[_MaritalStatusFilterIndex] is { } wantMarried && wantMarried != _MarriedIds.Contains(person.Id))
    {
      return false;
    }

    if (_IsYearFilterEnabled && !PersonLifetimeMatcher.IsAliveInYear(person, (int)_SelectedYear))
    {
      return false;
    }

    return true;
  }
}
