using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Utils;

/// <summary>
/// Pure name/sex/marital-status/alive-in-year filter criteria and matching, owned and driven by
/// <c>PersonFilterView</c> (GT4.UI.App), which syncs its controls into this state and raises its own
/// change event; call <see cref="Matches"/> from a page's own <c>FilteredObservableCollection{T}</c>
/// predicate. Marital status requires a relatives lookup a page doesn't otherwise need, so it isn't
/// part of the person data a page loads up front -- the view calls <see cref="SetMarriedIds"/> lazily
/// (the first time the filter panel is shown) once it has fetched it.
/// </summary>
public sealed class PersonFilter
{
  private static readonly BiologicalSex?[] SexFilterValues = [null, BiologicalSex.Male, BiologicalSex.Female, BiologicalSex.Unknown];
  private static readonly bool?[] MaritalStatusFilterValues = [null, true, false];

  private int _SelectedYear;
  private HashSet<int> _MarriedIds = [];

  public PersonFilter(IBiologicalSexFormatter biologicalSexFormatter)
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

  public string[] SexFilterLabels { get; }

  public string[] MaritalStatusFilterLabels { get; }

  public string NameFilter { get; set; } = string.Empty;

  public int SexFilterIndex { get; set; }

  public int MaritalStatusFilterIndex { get; set; }

  public bool IsYearFilterEnabled { get; set; }

  public double SelectedYear
  {
    get => _SelectedYear;
    set => _SelectedYear = (int)Math.Floor(value);
  }

  public int MinYear { get; private set; }

  public int MaxYear { get; private set; }

  public bool IsAnyFilterActive =>
    !string.IsNullOrEmpty(NameFilter) ||
    SexFilterIndex != 0 ||
    MaritalStatusFilterIndex != 0 ||
    IsYearFilterEnabled;

  /// <summary>Recomputes the year slider's bounds (e.g. after a page (re)loads its person set),
  /// clamping SelectedYear back into range if it fell outside it.</summary>
  public void SetYearBounds(int min, int max)
  {
    MinYear = min;
    MaxYear = max;

    if (_SelectedYear < min || _SelectedYear > max)
    {
      _SelectedYear = max;
    }
  }

  /// <summary>Sets which persons (by Id) are married, e.g. from a lazily-fetched relatives lookup.</summary>
  public void SetMarriedIds(IEnumerable<int> marriedIds)
  {
    _MarriedIds = [.. marriedIds];
  }

  public void Clear()
  {
    NameFilter = string.Empty;
    SexFilterIndex = 0;
    MaritalStatusFilterIndex = 0;
    IsYearFilterEnabled = false;
    _SelectedYear = MaxYear;
  }

  /// <summary>Computes the year-slider bounds from a person set's known birth/death years, falling
  /// back to a century before the current year when nothing is known. Takes the base Person type
  /// (rather than PersonInfo) since that's all it needs, so a page's own RelativeInfo[] roots pass
  /// straight through via array covariance instead of needing a PersonInfo projection.</summary>
  public static (int Min, int Max) ComputeYearBounds(IReadOnlyCollection<Person> persons)
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
  public static async Task<(int[] MarriedIds, int MinYear, int MaxYear)> FetchMarriedAndYearBoundsAsync(
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
    // Guarded on NameFilter being set (unlike relying on WildcardMatcher.IsMatch(x, "") == true):
    // a person with zero Names (e.g. a bare relative row with no name data) would otherwise never
    // pass Any(...) even with no filter active.
    if (!string.IsNullOrEmpty(NameFilter) && !person.Names.Any(n => WildcardMatcher.IsMatch(n.Value, NameFilter)))
    {
      return false;
    }

    if (SexFilterValues[SexFilterIndex] is { } sex && person.BiologicalSex != sex)
    {
      return false;
    }

    if (MaritalStatusFilterValues[MaritalStatusFilterIndex] is { } wantMarried && wantMarried != _MarriedIds.Contains(person.Id))
    {
      return false;
    }

    if (IsYearFilterEnabled && !PersonLifetimeMatcher.IsAliveInYear(person, _SelectedYear))
    {
      return false;
    }

    return true;
  }
}
