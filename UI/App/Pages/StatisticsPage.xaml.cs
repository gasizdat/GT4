using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Items;
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
  // Not "_Statistics == ProjectStatistics.Empty": ProjectStatistics is a record, so an empty project
  // compares equal to Empty even once loaded, and every refresh would re-show the indicator.
  private bool _StatisticsLoaded;
  private ProjectInfo? _LastProjectInfo;

  public StatisticsPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _NameFormatter = nameFormatter;

    Loading = new PageLoading(_AlertService);
    InitializeComponent();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  public PageLoading Loading { get; }

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
        Loading.Run(_StatisticsLoaded, LoadStatisticsAsync);
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
      _StatisticsLoaded = true;
      this.RefreshView();
    }, _AlertService);
  }

  // See ProjectPage.OnNavigatedTo: reload only when a change was committed since this page last loaded.
  protected void OnNavigatedTo(object sender, NavigatedToEventArgs e)
  {
    if (_LastProjectInfo != _CurrentProjectProvider.Info)
    {
      Refresh();
    }
  }

  // No XAML element binds to Statistics directly -- re-reading it here is what actually restarts
  // the load, not just flagging _UpdateStatistics and hoping something else re-reads it.
  private void Refresh()
  {
    _LastProjectInfo = _CurrentProjectProvider.Info;
    _UpdateStatistics = true;
    _ = Statistics;
  }

  // Built on every read, so each display property still goes through Statistics and its lazy load.
  private ProjectStatisticsText StatisticsText => new(Statistics, _NameFormatter);

  private static BirthDecadeItem ToBirthDecadeItem((string Decade, int Count) births, double rowLength)
  {
    var filled = new GridLength(births.Count, GridUnitType.Star);
    var rest = new GridLength(rowLength - births.Count, GridUnitType.Star);
    var barColumn = new ColumnDefinition(filled);
    var restColumn = new ColumnDefinition(rest);
    var columns = new ColumnDefinitionCollection(barColumn, restColumn);

    return new BirthDecadeItem(births.Decade, births.Count.ToString(), columns);
  }

  // Rendered once to size the name column every row then binds to.
  public string WidestDecadeName
  {
    get
    {
      var widest = BirthsByDecade.MaxBy(b => b.Decade.Length);
      return widest?.Decade ?? string.Empty;
    }
  }

  public string TotalPersonsText => StatisticsText.TotalPersons;

  public string TotalFamiliesText => StatisticsText.TotalFamilies;

  public string MenCountText => StatisticsText.MenCount;

  public string WomenCountText => StatisticsText.WomenCount;

  public string UnknownSexCountText => StatisticsText.UnknownSexCount;

  public string LivingCountText => StatisticsText.LivingCount;

  public string AverageLifespanText => StatisticsText.AverageLifespan;

  public string Lifespan95thPercentileText => StatisticsText.Lifespan95thPercentile;

  public string OldestLivingText => StatisticsText.OldestLiving;

  public string LongestLifespanText => StatisticsText.LongestLifespan;

  public string BirthYearSpanText => StatisticsText.BirthYearSpan;

  public string MedianBirthYearText => StatisticsText.MedianBirthYear;

  public BirthDecadeItem[] BirthsByDecade
  {
    get
    {
      var text = StatisticsText;
      var rowLength = text.DecadeRowLength;
      return [.. text.BirthsByDecade.Select(births => ToBirthDecadeItem(births, rowLength))];
    }
  }

  public string TopLargestFamiliesText => StatisticsText.TopLargestFamilies;

  public string SingleMemberFamiliesText => StatisticsText.SingleMemberFamilies;

  public string TopMaleFirstNamesText => StatisticsText.TopMaleFirstNames;

  public string TopFemaleFirstNamesText => StatisticsText.TopFemaleFirstNames;

  public string IncompleteBirthDateCountText => StatisticsText.IncompleteBirthDateCount;

  public string PhotoCoverageText => StatisticsText.PhotoCoverage;

  public string IsolatedPersonCountText => StatisticsText.IsolatedPersonCount;

  public string MarriageCountText => StatisticsText.MarriageCount;

  public string AverageChildrenText => StatisticsText.AverageChildren;

  public string MostChildrenText => StatisticsText.MostChildren;
}
