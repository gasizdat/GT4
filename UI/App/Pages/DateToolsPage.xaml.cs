using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class DateToolsPage : ContentPage
{
  private static readonly DisplayCalendar[] Calendars = Enum.GetValues<DisplayCalendar>();

  private readonly IDateFormatter _DateFormatter;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IAlertService _AlertService;
  private DisplayCalendar _ConvertFromCalendar = DisplayCalendar.Gregorian;
  private DisplayCalendar _ConvertToCalendar;
  private Date? _ConvertInput = Date.Now;
  private Date? _ConvertResult;
  private string _ConvertResultText = string.Empty;
  private DisplayCalendar _PeriodFromCalendar = DisplayCalendar.Gregorian;
  private DisplayCalendar _PeriodToCalendar = DisplayCalendar.Gregorian;
  private Date? _PeriodFrom = Date.Now;
  private Date? _PeriodTo = Date.Now;

  public DateToolsPage(
    IDateFormatter dateFormatter,
    IDateSpanFormatter dateSpanFormatter,
    IAlertService alertService,
    [FromKeyedServices(SettingKeys.Calendar)]
    ISettingEditor calendarSetting)
  {
    _DateFormatter = dateFormatter;
    _DateSpanFormatter = dateSpanFormatter;
    _AlertService = alertService;
    var calendar = CalendarConversion.ToDisplayCalendar(calendarSetting.Value);
    _ConvertToCalendar = calendar == DisplayCalendar.Gregorian ? DisplayCalendar.Julian : calendar;

    InitializeComponent();
  }

  public ICommand PageCommand => new SafeCommand((object arg) =>
  {
    switch (arg)
    {
      case string commandName when commandName == "Convert":
        Convert();
        break;
      case string commandName when commandName == "Swap":
        Swap();
        break;
    }
  }, _AlertService);

  public string[] CalendarNames => [.. Calendars.Select(_DateFormatter.CalendarName)];

  public DisplayCalendar ConvertFromCalendar
  {
    get => _ConvertFromCalendar;
    set
    {
      if (_ConvertFromCalendar != value)
      {
        _ConvertFromCalendar = value;
        OnPropertyChanged(nameof(ConvertFromCalendar));
        ClearConvertResult();
      }
    }
  }

  public Date? ConvertInput
  {
    get => _ConvertInput;
    set
    {
      if (_ConvertInput != value)
      {
        _ConvertInput = value;
        OnPropertyChanged(nameof(ConvertInput));
        ClearConvertResult();
      }
    }
  }

  public DisplayCalendar ConvertToCalendar
  {
    get => _ConvertToCalendar;
    set
    {
      if (_ConvertToCalendar != value)
      {
        _ConvertToCalendar = value;
        OnPropertyChanged(nameof(ConvertToCalendar));
        OnPropertyChanged(nameof(ConvertToCalendarIndex));
        ClearConvertResult();
      }
    }
  }

  public int ConvertToCalendarIndex
  {
    get => Array.IndexOf(Calendars, _ConvertToCalendar);
    set => ConvertToCalendar = Calendars[value];
  }

  public string ConvertResultText
  {
    get => _ConvertResultText;
    private set
    {
      _ConvertResultText = value;
      OnPropertyChanged(nameof(ConvertResultText));
    }
  }

  public DisplayCalendar PeriodFromCalendar
  {
    get => _PeriodFromCalendar;
    set
    {
      _PeriodFromCalendar = value;
      OnPropertyChanged(nameof(PeriodText));
    }
  }

  public DisplayCalendar PeriodToCalendar
  {
    get => _PeriodToCalendar;
    set
    {
      _PeriodToCalendar = value;
      OnPropertyChanged(nameof(PeriodText));
    }
  }

  public Date? PeriodFrom
  {
    get => _PeriodFrom;
    set
    {
      _PeriodFrom = value;
      OnPropertyChanged(nameof(PeriodText));
    }
  }

  public Date? PeriodTo
  {
    get => _PeriodTo;
    set
    {
      _PeriodTo = value;
      OnPropertyChanged(nameof(PeriodText));
    }
  }

  // Empty for the same date twice: a zero span has nothing to report.
  public string PeriodText
  {
    get
    {
      if (_PeriodFrom is not Date from)
        return NotInCalendar(UIStrings.DateToolsInputNotRepresentable_2, _PeriodFromCalendar);
      if (_PeriodTo is not Date to)
        return NotInCalendar(UIStrings.DateToolsInputNotRepresentable_2, _PeriodToCalendar);
      if (from == to)
        return string.Empty;

      var span = to - from;
      var spanText = _DateSpanFormatter.ToString(span);
      var totalDays = (ToDateTime(to) - ToDateTime(from)).Days;
      return string.Format(UIStrings.DateToolsPeriodResult_2, spanText, Math.Abs(totalDays));
    }
  }

  private void Convert()
  {
    if (_ConvertInput is not Date input)
    {
      ConvertResultText = NotInCalendar(UIStrings.DateToolsInputNotRepresentable_2, _ConvertFromCalendar);
      return;
    }

    var (applied, _, _, _) = CalendarConversion.TryConvert(input, _ConvertToCalendar);
    if (applied != _ConvertToCalendar)
    {
      ConvertResultText = NotInCalendar(UIStrings.DateToolsResultNotRepresentable_2, _ConvertToCalendar);
      return;
    }

    ConvertResultText = _DateFormatter.ToString(input, _ConvertToCalendar);
    _ConvertResult = input;
  }

  // Each setter clears the result, so it is taken first.
  private void Swap()
  {
    var result = _ConvertResult;
    var from = _ConvertFromCalendar;
    ConvertFromCalendar = _ConvertToCalendar;
    ConvertToCalendar = from;
    if (result is not null)
    {
      ConvertInput = result;
    }
  }

  private void ClearConvertResult()
  {
    _ConvertResult = null;
    ConvertResultText = string.Empty;
  }

  private string NotInCalendar(string format, DisplayCalendar calendar)
  {
    var name = _DateFormatter.CalendarName(calendar);
    var range = calendar switch
    {
      DisplayCalendar.Hebrew => UIStrings.DateToolsRangeHebrew,
      DisplayCalendar.FrenchRepublican => UIStrings.DateToolsRangeFrenchRepublican,
      _ => UIStrings.DateToolsRangeCommon,
    };
    return string.Format(format, name, range);
  }

  private static DateTime ToDateTime(Date date) => new(date.Year, date.Month, date.Day);
}
