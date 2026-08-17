using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
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

  private readonly HashSet<DateCalendarEventType> _ActiveTypes = [DateCalendarEventType.Birth, DateCalendarEventType.Death, DateCalendarEventType.Wedding];
  private PersonInfo[] _Persons = [];
  private Dictionary<int, Relative[]> _RelativesByPersonId = [];
  private int _UnplaceableCount;
  private int _DisplayMonth = Date.Now.Month;
  private bool _MilestoneOnly;
  private bool _UpdateData = true;
  private ProjectInfo? _LastProjectInfo;

  public DateCalendarPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _NameFormatter = nameFormatter;

    PageCommand = new SafeCommand(OnPageCommand, _AlertService);
    InitializeComponent();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  public ICommand PageCommand { get; init; }

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

      return DateCalendarCalculator.Compute(_Persons, _RelativesByPersonId, _DisplayMonth, Date.Now.Year);
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
    [.. entries.Where(PassesFilter).Select(e => new DateCalendarEntryItem(FormatEntryText(e), e.Type, e.IsMilestone))];

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

  public Style BirthsChipStyle => GetChipStyle(IsBirthsActive);

  public Style DeathsChipStyle => GetChipStyle(IsDeathsActive);

  public Style WeddingsChipStyle => GetChipStyle(IsWeddingsActive);

  public Style MilestoneOnlyChipStyle => GetChipStyle(IsMilestoneOnly);

  // Bound directly to the Button's Style property (rather than a Style.Triggers DataTrigger, which
  // does not re-apply BackgroundColor/TextColor after a native Windows Button click) so RefreshView's
  // property-changed notification reliably swaps the whole style.
  private Style GetChipStyle(bool isActive) => (Style)Resources[isActive ? "DateCalendarChipActive" : "DateCalendarChip"];

  public string DateCalendarTodayButtonName => string.Format(UIStrings.DateCalendarTodayButton_1, Date.Now.Day);
}
