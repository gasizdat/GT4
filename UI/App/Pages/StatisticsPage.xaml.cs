using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Pages;

public partial class StatisticsPage : ContentPage
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly INameFormatter _NameFormatter;

  private ProjectStatistics _Statistics = ProjectStatistics.Empty;
  private bool _UpdateStatistics = true;

  public StatisticsPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter,
    IProjectRevisionMonitor projectRevisionMonitor)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _NameFormatter = nameFormatter;

    InitializeComponent();
    Loaded += (_, _) => projectRevisionMonitor.RevisionChanged += OnRevisionChanged;
    Unloaded += (_, _) => projectRevisionMonitor.RevisionChanged -= OnRevisionChanged;
  }

  // The single trigger for the (lazy, async) load: every display property below reads Statistics, so
  // whichever one XAML binds first kicks off the load, following the same lazy-getter idiom as
  // NamesPage.Names / ProjectPage.Families.
  public ProjectStatistics Statistics
  {
    get
    {
      if (_UpdateStatistics)
      {
        _UpdateStatistics = false;
        SafeTask.Run(LoadStatisticsAsync, _AlertService);
      }

      return _Statistics;
    }
  }

  private async Task LoadStatisticsAsync()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var project = _CurrentProjectProvider.Project;

    var persons = await project.PersonManager.GetPersonInfosAsync(selectMainPhoto: true, token);
    var familyNames = await project.FamilyManager.GetFamiliesAsync(token);
    var relativesByPersonId = await project.Relatives.GetRelativesForPersonsAsync(persons, token);

    var statistics = ProjectStatisticsCalculator.Compute(persons, familyNames, relativesByPersonId);

    await SafeTask.RunOnMainThread(() =>
    {
      _Statistics = statistics;
      this.RefreshView();
    }, _AlertService);
  }

  protected void OnNavigatedTo(object sender, NavigatedToEventArgs e) => Refresh();

  private void OnRevisionChanged(object? sender, EventArgs e) => Refresh();

  // No XAML element binds to Statistics directly (the bound properties are the formatted *Text
  // ones derived from it), so re-reading it here -- rather than only flagging it and hoping
  // something else re-reads it -- is what actually restarts the load; LoadStatisticsAsync's own
  // RefreshView() on completion is what then updates the bound text properties.
  private void Refresh()
  {
    _UpdateStatistics = true;
    _ = Statistics;
  }

  private static string FormatYears(double? years) =>
    years is { } value ? string.Format(UIStrings.StatValueYears_1, value.ToString("F1")) : UIStrings.StatValueNone;

  private string FormatPersonYears(PersonInfo? person, int? years) =>
    person is not null
      ? string.Format(UIStrings.StatValuePersonYears_2, _NameFormatter.ToString(person, NameFormat.CommonPersonName), years)
      : UIStrings.StatValueNone;

  private static string FormatNameCounts((string Name, int Count)[] items) =>
    items.Length > 0
      ? string.Join(", ", items.Select(item => string.Format(UIStrings.StatValueNameCount_2, item.Name, item.Count)))
      : UIStrings.StatValueNone;

  public string TotalPersonsText => Statistics.TotalPersons.ToString();

  public string TotalFamiliesText => Statistics.TotalFamilies.ToString();

  public string MenCountText => Statistics.MenCount.ToString();

  public string WomenCountText => Statistics.WomenCount.ToString();

  public string UnknownSexCountText => Statistics.UnknownSexCount.ToString();

  public string LivingCountText => Statistics.LivingCount.ToString();

  public string AverageLifespanText => FormatYears(Statistics.AverageLifespanYears);

  public string Lifespan95thPercentileText => FormatYears(Statistics.Lifespan95thPercentileYears);

  public string OldestLivingText => FormatPersonYears(Statistics.OldestLivingPerson, Statistics.OldestLivingAgeYears);

  public string LongestLifespanText => FormatPersonYears(Statistics.LongestLifespanPerson, Statistics.LongestLifespanYears);

  public string BirthYearSpanText => Statistics.EarliestBirthYear is not null && Statistics.LatestBirthYear is not null
    ? string.Format(UIStrings.StatValueYearRange_2, Statistics.EarliestBirthYear, Statistics.LatestBirthYear)
    : UIStrings.StatValueNone;

  public string MedianBirthYearText => Statistics.MedianBirthYear?.ToString() ?? UIStrings.StatValueNone;

  public string BirthsByDecadeText => Statistics.BirthsByDecade.Length > 0
    ? string.Join("\n", Statistics.BirthsByDecade.Select(d => string.Format(UIStrings.StatValueDecadeCount_2, d.Decade, d.Count)))
    : UIStrings.StatValueNone;

  public string TopLargestFamiliesText => FormatNameCounts(Statistics.TopLargestFamilies);

  public string SingleMemberFamiliesText => Statistics.SingleMemberFamilyNames.Length > 0
    ? string.Join(", ", Statistics.SingleMemberFamilyNames)
    : UIStrings.StatValueNone;

  public string TopMaleFirstNamesText => FormatNameCounts(Statistics.TopMaleFirstNames);

  public string TopFemaleFirstNamesText => FormatNameCounts(Statistics.TopFemaleFirstNames);

  public string IncompleteBirthDateCountText => Statistics.IncompleteBirthDateCount.ToString();

  public string PhotoCoverageText => string.Format(UIStrings.StatValueCoverage_2, Statistics.PhotoCoverageCount, Statistics.TotalPersons);

  public string IsolatedPersonCountText => Statistics.IsolatedPersonCount.ToString();

  public string MarriageCountText => Statistics.MarriageCount.ToString();

  public string AverageChildrenText => Statistics.AverageChildrenPerParent is { } average
    ? string.Format(UIStrings.StatValueChildrenAverage_1, average.ToString("F1"))
    : UIStrings.StatValueNone;

  public string MostChildrenText => Statistics.MostChildrenPerson is { } person
    ? string.Format(UIStrings.StatValuePersonChildren_2, _NameFormatter.ToString(person, NameFormat.CommonPersonName), Statistics.MostChildrenCount)
    : UIStrings.StatValueNone;
}
