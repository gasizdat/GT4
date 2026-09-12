using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Resources;
using GT4.UI.Utils.Settings;

namespace GT4.UI.Utils.Formatters;

internal class DateFormatter : IDateFormatter
{
  private const string D2 = "D2";
  private readonly ISettingEditor _FullDateFormatSetting;
  private readonly ISettingEditor _ShortDateFormatSetting;
  private readonly ISettingEditor _CalendarSetting;

  public DateFormatter(
    [FromKeyedServices(DateFormatKind.Full)] ISettingEditor fullDateFormatSetting,
    [FromKeyedServices(DateFormatKind.Short)] ISettingEditor shortDateFormatSetting,
    [FromKeyedServices(SettingKeys.Calendar)] ISettingEditor calendarSetting)
  {
    _FullDateFormatSetting = fullDateFormatSetting;
    _ShortDateFormatSetting = shortDateFormatSetting;
    _CalendarSetting = calendarSetting;
  }

  public string ToString(Date? date)
  {
    if (date.HasValue)
    {
      string ret;
      var requestedCalendar = CalendarConversion.ToDisplayCalendar(_CalendarSetting.Value);
      var appliedCalendar = DisplayCalendar.Gregorian;

      switch (date.Value.Status)
      {
        case DateStatus.WellKnown:
          var (applied, year, month, day) = CalendarConversion.TryConvert(date.Value, requestedCalendar);
          var appliedDate = date.Value with { Year = year, Month = month, Day = day };
          ret = ToString(
            _FullDateFormatSetting.Value,
            () => YearToString(appliedDate, applied),
            () => MonthToString(appliedDate, applied),
            () => MonthToNumber(appliedDate),
            () => DayToString(appliedDate));
          appliedCalendar = applied;
          break;

        case DateStatus.DayUnknown:
          ret = GregorianFormat(_ShortDateFormatSetting.Value, date.Value);
          break;

        case DateStatus.MonthUnknown:
          ret = YearToString(date.Value, DisplayCalendar.Gregorian);
          break;

        case DateStatus.YearApproximate:
          ret = YearToString(date.Value, DisplayCalendar.Gregorian);
          ret = string.Format(UIStrings.DateStatusYearApproximate_1, ret);
          break;

        case DateStatus.Unknown:
          ret = UIStrings.DateStatusUnknown;
          appliedCalendar = requestedCalendar;
          break;

        default:
          ret = $"⚠ Unexpected DateStatus={date.Value.Status}";
          break;
      }

      if (requestedCalendar != DisplayCalendar.Gregorian)
      {
        ret = string.Format(UIStrings.DateCalendarSuffix_1, ret, CalendarLabel(appliedCalendar));
      }

      return ret;
    }
    else
    {
      return UIStrings.DateStatusNotDefined;
    }
  }

  /// <summary>Stateless, so callers that already hold the format they want (e.g. a setting previewing
  /// its own configured value) don't need an <see cref="IDateFormatter"/> instance to use it.</summary>
  public static string GregorianFormat(string format, Date date) =>
    ToString(
      format,
      () => YearToString(date, DisplayCalendar.Gregorian),
      () => MonthToString(date, DisplayCalendar.Gregorian),
      () => MonthToNumber(date),
      () => DayToString(date)
    );

  public static string CalendarLabel(DisplayCalendar calendar) => calendar switch
  {
    DisplayCalendar.Gregorian => UIStrings.FieldCalendarGregorian,
    DisplayCalendar.Julian => UIStrings.FieldCalendarJulian,
    DisplayCalendar.Hebrew => UIStrings.FieldCalendarHebrew,
    DisplayCalendar.FrenchRepublican => UIStrings.FieldCalendarFrenchRepublican,
    _ => throw new NotImplementedException($"DisplayCalendar={calendar}")
  };

  protected static string YearToString(Date date, DisplayCalendar displayCalendar)
  {
    var ret = displayCalendar switch
    {
      DisplayCalendar.Gregorian or
      DisplayCalendar.Julian => GregorianYears(date),
      DisplayCalendar.Hebrew or
      DisplayCalendar.FrenchRepublican => NonGregorianYears(date),
      _ => throw new NotImplementedException($"DisplayCalendar={displayCalendar}")
    };

    return ret;
  }

  protected static string GregorianYears(Date date)
  {
    var ret = date.Year.ToString();
    if (date.Sign < 0)
    {
      ret = string.Format(UIStrings.DateEraBeforeChrist_1, ret);
    }

    return ret;
  }

  protected static string NonGregorianYears(Date date)
  {
    var ret = date.Year.ToString();

    return ret;
  }

  protected static string MonthToNumber(Date date)
  {
    var month = date.Month;
    var ret = month.ToString(D2);

    return ret;
  }

  protected static string MonthToString(Date date, DisplayCalendar displayCalendar)
  {
    var monthNames = displayCalendar switch
    {
      DisplayCalendar.Gregorian or
      DisplayCalendar.Julian => GregorianMonths,
      DisplayCalendar.Hebrew => HebrewMonths(date),
      DisplayCalendar.FrenchRepublican => FrenchRepublicanMonths,
      _ => throw new NotImplementedException($"DisplayCalendar={displayCalendar}")
    };

    if (date.Month < 1 || date.Month > monthNames.Length)
    {
      return MonthToNumber(date);
    }

    var monthName = monthNames[date.Month - 1];

    if (Language.Current == Language.RU)
    {
      monthName = monthName.ToLower();

      if (date.Status == DateStatus.WellKnown)
      {
        monthName = MonthGenitiveRU(monthName);
      }
    }

    return monthName;
  }

  protected static string[] GregorianMonths =>
    [
      UIStrings.Month_01,
      UIStrings.Month_02,
      UIStrings.Month_03,
      UIStrings.Month_04,
      UIStrings.Month_05,
      UIStrings.Month_06,
      UIStrings.Month_07,
      UIStrings.Month_08,
      UIStrings.Month_09,
      UIStrings.Month_10,
      UIStrings.Month_11,
      UIStrings.Month_12
    ];

  protected static string[] HebrewMonths(Date date) => CalendarConversion.IsHebrewLeapYear(date.Year) ?
    [
      UIStrings.MonthHebrew_Tishrei,
      UIStrings.MonthHebrew_Cheshvan,
      UIStrings.MonthHebrew_Kislev,
      UIStrings.MonthHebrew_Tevet,
      UIStrings.MonthHebrew_Shevat,
      UIStrings.MonthHebrew_AdarI,
      UIStrings.MonthHebrew_AdarII,
      UIStrings.MonthHebrew_Nisan,
      UIStrings.MonthHebrew_Iyar,
      UIStrings.MonthHebrew_Sivan,
      UIStrings.MonthHebrew_Tamuz,
      UIStrings.MonthHebrew_Av,
      UIStrings.MonthHebrew_Elul
    ] :
    [
      UIStrings.MonthHebrew_Tishrei,
      UIStrings.MonthHebrew_Cheshvan,
      UIStrings.MonthHebrew_Kislev,
      UIStrings.MonthHebrew_Tevet,
      UIStrings.MonthHebrew_Shevat,
      UIStrings.MonthHebrew_Adar,
      UIStrings.MonthHebrew_Nisan,
      UIStrings.MonthHebrew_Iyar,
      UIStrings.MonthHebrew_Sivan,
      UIStrings.MonthHebrew_Tamuz,
      UIStrings.MonthHebrew_Av,
      UIStrings.MonthHebrew_Elul,
    ];

  protected static string[] FrenchRepublicanMonths => FrenchRepublicanCalendar.MonthNames;

  // Only a Cyrillic word takes a Russian genitive ending; Latin-script month names reach here too.
  protected static string MonthGenitiveRU(string month)
  {
    if (month.Last() is not (>= 'а' and <= 'я' or 'ё'))
    {
      return month;
    }

    var ret = month.Last() switch
    {
      'ь' or 'й' => month.Substring(0, month.Length - 1) + "я",
      _ => month + "а"
    };

    return ret;
  }

  protected static string DayToString(Date date)
  {
    var ret = date.Day.ToString(D2);

    return ret;
  }

  protected static string ToString(string format, Func<string> year, Func<string> month, Func<string> monthNumber, Func<string> day)
  {
    var ret = TemplateInterpolator.Format(format, new Dictionary<string, Func<string>>()
    {
      { "YYYY", year},
      { "MM", monthNumber},
      { "MMM", month},
      { "DD", day},
    });

    return ret;
  }
}
