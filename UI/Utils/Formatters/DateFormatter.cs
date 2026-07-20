using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Settings;

namespace GT4.UI.Utils.Formatters;

internal class DateFormatter : IDateFormatter
{  
  private const string D2 = "D2";
  private readonly ISettingEditor _FullDateFormatSetting;
  private readonly ISettingEditor _ShortDateFormatSetting;

  public DateFormatter(
    [FromKeyedServices(DateFormatKind.Full)] ISettingEditor fullDateFormatSetting,
    [FromKeyedServices(DateFormatKind.Short)] ISettingEditor shortDateFormatSetting)
  {
    _FullDateFormatSetting = fullDateFormatSetting;
    _ShortDateFormatSetting = shortDateFormatSetting;
  }

  public string ToString(Date? date)
  {
    if (date.HasValue)
    {
      var year = () => YearToString(date.Value);
      return date.Value.Status switch
      {
        DateStatus.WellKnown => Format(_FullDateFormatSetting.Value, date.Value),
        DateStatus.DayUnknown => Format(_ShortDateFormatSetting.Value, date.Value),
        DateStatus.MonthUnknown => year(),
        DateStatus.YearApproximate => string.Format(UIStrings.DateStatusYearApproximate_1, year()),
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
  public static string Format(string format, Date date)
  {
    return ToString(format, () => YearToString(date), () => MonthToString(date), () => MonthToNumber(date), () => DayToString(date));
  }

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

  protected static string MonthToString(Date date)
  {
    var month = date.Month;
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

      if (date.Status == DateStatus.WellKnown)
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
}