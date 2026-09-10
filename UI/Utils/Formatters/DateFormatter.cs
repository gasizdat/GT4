using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Settings;

namespace GT4.UI.Utils.Formatters;

internal class DateFormatter : IDateFormatter
{
  private const string D2 = "D2";
  private readonly ISettingEditor _FullDateFormatSetting;
  private readonly ISettingEditor _ShortDateFormatSetting;
  private readonly ISettingEditor _CalendarSetting;

  // Hebrew leap years insert Adar I before Adar II, shifting every later month's number up by one --
  // CalendarConversion.IsHebrewLeapYear picks which of the two arrays a given year reads from.
  private static readonly string[] HebrewMonthsCommon =
    ["Tishrei", "Cheshvan", "Kislev", "Tevet", "Shevat", "Adar", "Nisan", "Iyar", "Sivan", "Tamuz", "Av", "Elul"];
  private static readonly string[] HebrewMonthsLeap =
    ["Tishrei", "Cheshvan", "Kislev", "Tevet", "Shevat", "Adar I", "Adar II", "Nisan", "Iyar", "Sivan", "Tamuz", "Av", "Elul"];

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
      return date.Value.Status switch
      {
        DateStatus.WellKnown => FormatWellKnown(date.Value),
        DateStatus.DayUnknown => WithCalendarLabel(Format(_ShortDateFormatSetting.Value, date.Value), DisplayCalendar.Gregorian),
        DateStatus.MonthUnknown => WithCalendarLabel(YearToString(date.Value), DisplayCalendar.Gregorian),
        DateStatus.YearApproximate => WithCalendarLabel(string.Format(UIStrings.DateStatusYearApproximate_1, YearToString(date.Value)), DisplayCalendar.Gregorian),
        DateStatus.Unknown => UIStrings.DateStatusUnknown,
        _ => $"⚠ Unexpected DateStatus={date.Value.Status}"
      };
    }
    else
    {
      return UIStrings.DateStatusNotDefined;
    }
  }

  /// <summary>Applies an arbitrary format string to a date, independent of any configured setting.
  /// Stateless, so callers that already hold the format they want (e.g. a setting previewing its own
  /// configured value) don't need an <see cref="IDateFormatter"/> instance to use it.</summary>
  public static string Format(string format, Date date) => ToString(format, () => YearToString(date), () => MonthToString(date), () => MonthToNumber(date), () => DayToString(date));

  public static string CalendarLabel(DisplayCalendar calendar) => calendar switch
  {
    DisplayCalendar.Gregorian => UIStrings.FieldCalendarGregorian,
    DisplayCalendar.Julian => UIStrings.FieldCalendarJulian,
    DisplayCalendar.Hebrew => UIStrings.FieldCalendarHebrew,
    DisplayCalendar.FrenchRepublican => UIStrings.FieldCalendarFrenchRepublican,
    _ => throw new NotImplementedException($"DisplayCalendar={calendar}")
  };

  protected static string YearToString(Date date)
  {
    var ret = date.Year.ToString();
    if (date.Sign < 0)
    {
      ret = string.Format(UIStrings.DateEraBeforeChrist_1, ret);
    }

    return ret;
  }
  protected static string MonthToNumber(Date date)
  {
    string ret;
    var month = date.Month;
    ret = month.ToString(D2);

    return ret;
  }

  protected static string MonthToString(Date date) => MonthToString(date.Month, date.Status);

  protected static string MonthToString(int month, DateStatus status)
  {
    var ret = month switch
    {
      1 => UIStrings.Month_01,
      2 => UIStrings.Month_02,
      3 => UIStrings.Month_03,
      4 => UIStrings.Month_04,
      5 => UIStrings.Month_05,
      6 => UIStrings.Month_06,
      7 => UIStrings.Month_07,
      8 => UIStrings.Month_08,
      9 => UIStrings.Month_09,
      10 => UIStrings.Month_10,
      11 => UIStrings.Month_11,
      12 => UIStrings.Month_12,
      _ => month.ToString(D2)
    };

    if (Language.Current == Language.RU)
    {
      ret = ret.ToLower();

      if (status == DateStatus.WellKnown)
      {
        ret = MonthGenitiveRU(ret);
      }
    }

    return ret;
  }

  protected static string MonthGenitiveRU(string month)
  {
    var ret = month.Last() switch
    {
      'ь' or 'й' => month.Substring(0, month.Length - 1) + "я",
      _ => month + "a"
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

  // The setting can ask for a calendar whose range doesn't cover this particular date (each
  // non-Gregorian calendar has one -- see CalendarConversion). Applied names what was actually used,
  // which is what the label below shows: never the raw request, so a silent fallback still tells the
  // user they're looking at a Gregorian date.
  private string FormatWellKnown(Date date)
  {
    var (applied, year, month, day) = CalendarConversion.TryConvert(date, CalendarConversion.Parse(_CalendarSetting.Value));
    var text = applied == DisplayCalendar.Gregorian
      ? Format(_FullDateFormatSetting.Value, date)
      : ToString(_FullDateFormatSetting.Value, () => year.ToString(), () => MonthLabel(applied, year, month), () => month.ToString(D2), () => day.ToString(D2));

    return WithCalendarLabel(text, applied);
  }

  // A partial date (DayUnknown/MonthUnknown/YearApproximate) never converts -- see
  // CalendarConversion.TryConvert -- but still needs the same "which calendar is this" label whenever
  // the setting isn't Gregorian, or it would look identical to a converted date instead of a fallback.
  private string WithCalendarLabel(string text, DisplayCalendar applied) =>
    CalendarConversion.Parse(_CalendarSetting.Value) == DisplayCalendar.Gregorian
      ? text
      : string.Format(UIStrings.DateCalendarSuffix_1, text, CalendarLabel(applied));

  private static string MonthLabel(DisplayCalendar applied, int year, int month) => applied switch
  {
    DisplayCalendar.Julian => MonthToString(month, DateStatus.WellKnown),
    DisplayCalendar.Hebrew => (CalendarConversion.IsHebrewLeapYear(year) ? HebrewMonthsLeap : HebrewMonthsCommon)[month - 1],
    DisplayCalendar.FrenchRepublican => FrenchRepublicanCalendar.MonthAbbreviations[month - 1],
    _ => throw new NotImplementedException($"DisplayCalendar={applied}")
  };
}
