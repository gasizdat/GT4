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
    [FromKeyedServices(nameof(FullDateFormatSetting))] ISettingEditor fullDateFormatSetting,
    [FromKeyedServices(nameof(ShortDateFormatSetting))] ISettingEditor shortDateFormatSetting)
  {
    _FullDateFormatSetting = fullDateFormatSetting;
    _ShortDateFormatSetting = shortDateFormatSetting;
  }

  public string ToString(Date? date)
  {
    // TODO Apply Date.Sign

    if (date.HasValue)
    {
      var year = () => YearToString(date.Value);
      var month = () => MonthToString(date.Value);
      var monthNumber = () => MonthToNumber(date.Value);
      var day = () => DayToString(date.Value);
      return date.Value.Status switch
      {
        DateStatus.WellKnown => ToString(_FullDateFormatSetting.Value, year, month, monthNumber, day),
        DateStatus.DayUnknown => ToString(_ShortDateFormatSetting.Value, year, month, monthNumber, day),
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

  protected static string YearToString(Date date)
  {
    var ret = date.Year.ToString();

    return ret;
  }
  protected string MonthToNumber(Date date)
  {
    string ret;
    var month = date.Month;
    ret = month.ToString(D2);

    return ret;
  }

  protected string MonthToString(Date date)
  {
    string ret;
    var month = date.Month;
    if (Language.Current == Language.RU)
    {
      ret = month switch
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

      ret = ret.ToLower();

      if (date.Status == DateStatus.WellKnown)
      {
        ret = MonthGenitiveRU(ret);
      }
    }
    else
    {
      ret = month switch
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