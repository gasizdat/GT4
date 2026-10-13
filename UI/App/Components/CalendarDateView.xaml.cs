using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Components;

public partial class CalendarDateView : ContentView
{
  private const string D2 = "D2";
  private static readonly DisplayCalendar[] Calendars = Enum.GetValues<DisplayCalendar>();

  private readonly IDateFormatter _DateFormatter;
  private int _Year;
  private int _Month = 1;
  private int _Day = 1;

  protected CalendarDateView(IServiceProvider serviceProvider)
  {
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    InitializeComponent();
  }

  public CalendarDateView()
    : this(GT4Services.Provider)
  {

  }

  public static readonly BindableProperty CalendarProperty = BindableProperty.Create(
    nameof(Calendar),
    typeof(DisplayCalendar),
    typeof(CalendarDateView),
    DisplayCalendar.Gregorian,
    BindingMode.TwoWay,
    null,
    OnCalendarChanged);

  public static readonly BindableProperty DateProperty = BindableProperty.Create(
    nameof(Date),
    typeof(Date?),
    typeof(CalendarDateView),
    null,
    BindingMode.TwoWay,
    null,
    OnDateChanged);

  // The entered numbers are kept, so a calendar change reinterprets them rather than converting.
  private static void OnCalendarChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is CalendarDateView view)
    {
      view.SetParts(view._Year, view._Month, view._Day);
    }
  }

  private static void OnDateChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is not CalendarDateView view || newValue is not Date date || view.Entered == date)
      return;

    var (applied, year, month, day) = CalendarConversion.TryConvert(date, view.Calendar);
    if (applied == view.Calendar)
    {
      view.SetParts(year, month, day);
      view.OnPropertyChanged(nameof(Year));
    }
  }

  public DisplayCalendar Calendar
  {
    get => (DisplayCalendar)GetValue(CalendarProperty);
    set => SetValue(CalendarProperty, value);
  }

  /// <summary>The Gregorian date entered, <c>null</c> while it does not exist in <see cref="Calendar"/>
  /// or falls outside its range.</summary>
  public Date? Date
  {
    get => (Date?)GetValue(DateProperty);
    set => SetValue(DateProperty, value);
  }

  public string[] CalendarNames => [.. Calendars.Select(_DateFormatter.CalendarName)];

  public int CalendarIndex
  {
    get => Array.IndexOf(Calendars, Calendar);
    set => Calendar = Calendars[value];
  }

  public string Year
  {
    get => _Year.ToString();
    set
    {
      var year = int.TryParse(value, out var parsed) ? parsed : 0;
      if (year == _Year)
        return;

      var month = Calendar == DisplayCalendar.Hebrew
        ? CalendarConversion.HebrewMonthInYear(_Month, LookupYear(_Year), LookupYear(year))
        : _Month;
      SetParts(year, month, _Day);
    }
  }

  public string[] Months
  {
    get
    {
      var names = _DateFormatter.MonthNames(Calendar, LookupYear(_Year));
      return [.. names.Select((name, index) => $"{(index + 1).ToString(D2)} - {name}")];
    }
  }

  public int MonthIndex
  {
    get => _Month - 1;
    set => SetParts(_Year, value + 1, _Day);
  }

  public string[] Days
  {
    get
    {
      var count = CalendarConversion.DaysInMonth(Calendar, LookupYear(_Year), _Month);
      var days = Enumerable.Range(1, count);
      return [.. days.Select(day => day.ToString(D2))];
    }
  }

  public int DayIndex
  {
    get => _Day - 1;
    set => SetParts(_Year, _Month, value + 1);
  }

  private Date? Entered => CalendarConversion.ToGregorian(Calendar, _Year, _Month, _Day);

  // The month and day lists still need a year the calendar's lookups accept while the entered one
  // is out of range, e.g. halfway through typing it.
  private int LookupYear(int year)
  {
    var (min, max) = CalendarConversion.YearRange(Calendar);
    return Math.Clamp(year, min, max);
  }

  private void SetParts(int year, int month, int day)
  {
    var lookupYear = LookupYear(year);
    var months = CalendarConversion.MonthsInYear(Calendar, lookupYear);
    _Year = year;
    _Month = Math.Clamp(month, 1, months);
    var days = CalendarConversion.DaysInMonth(Calendar, lookupYear, _Month);
    _Day = Math.Clamp(day, 1, days);

    OnPropertyChanged(nameof(CalendarIndex));
    OnPropertyChanged(nameof(Months));
    OnPropertyChanged(nameof(MonthIndex));
    OnPropertyChanged(nameof(Days));
    OnPropertyChanged(nameof(DayIndex));
    Date = Entered;
  }
}
