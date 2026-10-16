using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Components;

public partial class CalendarDateView : ContentView
{
  private const string D2 = "D2";
  private static readonly DisplayCalendar[] Calendars = Enum.GetValues<DisplayCalendar>();

  private readonly IDateFormatter _DateFormatter;
  // Unclamped: a year typed one digit at a time passes through years the picked month and day don't fit.
  private int _Year;
  private int _Month = 1;
  private int _Day = 1;
  private int _MonthNumberingYear;
  private bool _Refreshing;

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
      view._MonthNumberingYear = view.LookupYear(view._Year);
      view.Refresh();
    }
  }

  private static void OnDateChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is not CalendarDateView view || newValue is not Date date || view.Entered == date)
      return;

    var (applied, year, month, day) = CalendarConversion.TryConvert(date, view.Calendar);
    if (applied == view.Calendar)
    {
      (view._Year, view._Month, view._Day, view._MonthNumberingYear) = (year, month, day, year);
      view.Refresh();
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

      _Year = year;
      var (min, max) = CalendarConversion.YearRange(Calendar);
      if (Calendar == DisplayCalendar.Hebrew && year >= min && year <= max)
      {
        _Month = CalendarConversion.HebrewMonthInYear(_Month, _MonthNumberingYear, year);
        _MonthNumberingYear = year;
      }

      Refresh();
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
    get => Month - 1;
    set
    {
      if (!_Refreshing)
      {
        _Month = value + 1;
        _MonthNumberingYear = LookupYear(_Year);
        Refresh();
      }
    }
  }

  public string[] Days
  {
    get
    {
      var count = CalendarConversion.DaysInMonth(Calendar, LookupYear(_Year), Month);
      var days = Enumerable.Range(1, count);
      return [.. days.Select(day => day.ToString(D2))];
    }
  }

  public int DayIndex
  {
    get => Day - 1;
    set
    {
      if (!_Refreshing)
      {
        _Day = value + 1;
        Refresh();
      }
    }
  }

  private int Month
  {
    get
    {
      var months = CalendarConversion.MonthsInYear(Calendar, LookupYear(_Year));
      return Math.Clamp(_Month, 1, months);
    }
  }

  private int Day
  {
    get
    {
      var days = CalendarConversion.DaysInMonth(Calendar, LookupYear(_Year), Month);
      return Math.Clamp(_Day, 1, days);
    }
  }

  private Date? Entered => CalendarConversion.ToGregorian(Calendar, _Year, Month, Day);

  // The calendar's lookups throw outside its range, which a year passes through while typed.
  private int LookupYear(int year)
  {
    var (min, max) = CalendarConversion.YearRange(Calendar);
    return Math.Clamp(year, min, max);
  }

  private void Refresh()
  {
    // A shorter list clamps a picker's selection, and the picker pushes the clamped index back.
    _Refreshing = true;
    try
    {
      OnPropertyChanged(nameof(CalendarIndex));
      OnPropertyChanged(nameof(Months));
      OnPropertyChanged(nameof(MonthIndex));
      OnPropertyChanged(nameof(Days));
      OnPropertyChanged(nameof(DayIndex));
    }
    finally
    {
      _Refreshing = false;
    }

    Date = Entered;
  }
}
