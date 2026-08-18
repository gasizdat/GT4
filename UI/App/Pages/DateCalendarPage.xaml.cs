using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
using System.Globalization;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class DateCalendarPage : ContentPage
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly INameFormatter _NameFormatter;
  private readonly INavigationService _NavigationService;

  private readonly HashSet<DateCalendarEventType> _ActiveTypes = [DateCalendarEventType.Birth, DateCalendarEventType.Death, DateCalendarEventType.Wedding];
  private PersonInfo[] _Persons = [];
  private Dictionary<int, Relative[]> _RelativesByPersonId = [];
  private Dictionary<int, Data?> _MainPhotosByPersonId = [];
  private int _UnplaceableCount;
  private int _DisplayMonth = Date.Now.Month;
  private int? _PhotosLoadedMonth;
  private bool _PersonsLoaded;
  private bool _MilestoneOnly;
  private bool _UpdateData = true;
  private ProjectInfo? _LastProjectInfo;

  public DateCalendarPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter,
    INavigationService navigationService)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _NameFormatter = nameFormatter;
    _NavigationService = navigationService;

    PageCommand = new SafeCommand(OnPageCommand, _AlertService);
    PersonCommand = new SafeCommand<PersonInfo>(OnOpenPerson, _AlertService);
    InitializeComponent();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  public ICommand PageCommand { get; init; }

  public ICommand PersonCommand { get; init; }

  protected async Task OnOpenPerson(PersonInfo person) =>
    await _NavigationService.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = person });

  // The single trigger for the (lazy, async) load: every display property below reads through
  // CalendarMonth, following the same lazy-getter idiom as StatisticsPage.Statistics.
  public DateCalendarMonth CalendarMonth
  {
    get
    {
      if (_UpdateData)
      {
        _UpdateData = false;
        SafeTask.Run(LoadCalendarDataAsync, _AlertService);
      }

      var month = DateCalendarCalculator.Compute(_Persons, _RelativesByPersonId, _DisplayMonth, Date.Now.Year);
      EnsureMonthPhotosLoaded(month);
      return month;
    }
  }

  private async Task LoadCalendarDataAsync()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var project = _CurrentProjectProvider.Project;

    var persons = await project.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, token);
    var relativesByPersonId = await project.Relatives.GetRelativesForPersonsAsync(persons, token);
    var unplaceableCount = DateCalendarCalculator.CountUnplaceable(persons, relativesByPersonId);

    await SafeTask.RunOnMainThread(() =>
    {
      _Persons = persons;
      _RelativesByPersonId = relativesByPersonId;
      _UnplaceableCount = unplaceableCount;
      _PersonsLoaded = true;
      this.RefreshView();
    }, _AlertService);
  }

  // Bounded to the persons actually shown in the displayed month (typically a handful) rather than
  // the whole project's main photos, since _Persons above is fetched unphotoed for every project
  // member just to place calendar entries. Re-fetched on every month change (PrevMonth/NextMonth/
  // Today), never on filter toggles alone: filtering only hides already-computed entries. Waits on
  // _PersonsLoaded so the empty CalendarMonth returned before the base load lands doesn't get
  // mistaken for "this month genuinely has no entries" and permanently skip the real fetch.
  private void EnsureMonthPhotosLoaded(DateCalendarMonth month)
  {
    if (!_PersonsLoaded || _PhotosLoadedMonth == _DisplayMonth)
      return;

    _PhotosLoadedMonth = _DisplayMonth;
    var persons = month.Days.SelectMany(d => d.Entries).Concat(month.DayUnknown).Select(e => e.Person).DistinctBy(p => p.Id).ToArray();
    if (persons.Length > 0)
    {
      SafeTask.Run(() => LoadMonthPhotosAsync(persons), _AlertService);
    }
  }

  private async Task LoadMonthPhotosAsync(PersonInfo[] persons)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var photosByPersonId = await _CurrentProjectProvider.Project.PersonData.GetMergedPhotoSetAsync(persons, DataCategory.PersonMainPhoto, token);

    await SafeTask.RunOnMainThread(() =>
    {
      _MainPhotosByPersonId = photosByPersonId.ToDictionary(p => p.Key, p => p.Value.FirstOrDefault());
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

  private void Refresh()
  {
    _LastProjectInfo = _CurrentProjectProvider.Info;
    _UpdateData = true;
    _PersonsLoaded = false;
    _PhotosLoadedMonth = null;
    this.RefreshView();
  }

  private void OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "PrevMonth":
        _DisplayMonth = _DisplayMonth == 1 ? 12 : _DisplayMonth - 1;
        this.RefreshView();
        break;

      case string commandName when commandName == "NextMonth":
        _DisplayMonth = _DisplayMonth == 12 ? 1 : _DisplayMonth + 1;
        this.RefreshView();
        break;

      case string commandName when commandName == "Today":
        _DisplayMonth = Date.Now.Month;
        this.RefreshView();
        break;

      case string commandName when commandName == "ToggleBirths":
        ToggleType(DateCalendarEventType.Birth);
        break;

      case string commandName when commandName == "ToggleDeaths":
        ToggleType(DateCalendarEventType.Death);
        break;

      case string commandName when commandName == "ToggleWeddings":
        ToggleType(DateCalendarEventType.Wedding);
        break;

      case string commandName when commandName == "ToggleMilestoneOnly":
        _MilestoneOnly = !_MilestoneOnly;
        this.RefreshView();
        break;
    }
  }

  private void ToggleType(DateCalendarEventType type)
  {
    if (!_ActiveTypes.Remove(type))
    {
      _ActiveTypes.Add(type);
    }

    this.RefreshView();
  }

  private bool PassesFilter(DateCalendarEntry entry) =>
    _ActiveTypes.Contains(entry.Type) && (!_MilestoneOnly || entry.IsMilestone);

  private string FormatEntryText(DateCalendarEntry entry) => entry.Type switch
  {
    DateCalendarEventType.Birth => string.Format(
      entry.Person.DeathDate != null ? UIStrings.DateCalendarBirthdayDeceasedText_2 : UIStrings.DateCalendarBirthdayText_2,
      _NameFormatter.ToString(entry.Person, NameFormat.CommonPersonName),
      entry.Years),
    DateCalendarEventType.Death => string.Format(
      UIStrings.DateCalendarDeathAnniversaryText_2, _NameFormatter.ToString(entry.Person, NameFormat.CommonPersonName), entry.Years),
    DateCalendarEventType.Wedding => string.Format(
      UIStrings.DateCalendarWeddingAnniversaryText_3,
      _NameFormatter.ToString(entry.Person, NameFormat.CommonPersonName),
      _NameFormatter.ToString(entry.Spouse!, NameFormat.CommonPersonName),
      entry.Years),
    _ => throw new NotImplementedException($"Type: {entry.Type}"),
  };

  private DateCalendarEntryItem[] FormatEntries(IEnumerable<DateCalendarEntry> entries) =>
    [.. entries.Where(PassesFilter).Select(e => new DateCalendarEntryItem(FormatEntryText(e), e.Type, e.IsMilestone, WithMainPhoto(e.Person)))];

  private PersonInfo WithMainPhoto(PersonInfo person) =>
    _MainPhotosByPersonId.TryGetValue(person.Id, out var photo) ? person with { MainPhoto = photo } : person;

  public string MonthLabel => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(_DisplayMonth);

  public DateCalendarDayItem[] DayGroups =>
    [.. CalendarMonth.Days
      .Select(d => new DateCalendarDayItem(d.Day, _DisplayMonth == Date.Now.Month && d.Day == Date.Now.Day, FormatEntries(d.Entries)))
      .Where(d => d.Entries.Length > 0)];

  public DateCalendarEntryItem[] DayUnknownEntries => FormatEntries(CalendarMonth.DayUnknown);

  public bool IsDayUnknownVisible => DayUnknownEntries.Length > 0;

  public string DayUnknownHeaderText => string.Format(UIStrings.DateCalendarDayUnknownHeader_1, MonthLabel);

  public bool IsEmptyStateVisible => DayGroups.Length == 0 && DayUnknownEntries.Length == 0;

  public string EmptyStateText => string.Format(UIStrings.DateCalendarEmptyState_1, MonthLabel);

  public bool IsUnplaceableFootnoteVisible => _UnplaceableCount > 0;

  public string UnplaceableFootnoteText => string.Format(UIStrings.DateCalendarUnplaceableFootnote_1, _UnplaceableCount);

  public bool IsBirthsActive => _ActiveTypes.Contains(DateCalendarEventType.Birth);

  public bool IsDeathsActive => _ActiveTypes.Contains(DateCalendarEventType.Death);

  public bool IsWeddingsActive => _ActiveTypes.Contains(DateCalendarEventType.Wedding);

  public bool IsMilestoneOnly => _MilestoneOnly;

  public string DateCalendarTodayButtonName => string.Format(UIStrings.DateCalendarTodayButton_1, Date.Now.Day);
}
