using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace GT4.UI.Utils.Formatters;

public class DateFormatter : IDateFormatter
{
  private const string D4 = "D4";
  private const string D2 = "D2";
  private readonly Configuration.Date _Configuration;

  public DateFormatter(IConfiguration configuration)
  {
    var dateConfig = configuration.GetSection(Configuration.Date.SectionName).Value;
    var dto = dateConfig == null ? null : JsonSerializer.Deserialize<Configuration.Date>(dateConfig);
    _Configuration = dto ?? new();
  }

  public string ToString(Date? date)
  {
    // TODO Apply Date.Sign

    if (date.HasValue)
    {
      var year = YearToString(date.Value);
      var month = MonthToString(date.Value);
      var day = DayToString(date.Value);
      return date.Value.Status switch
      {
        DateStatus.WellKnown => ToString(_Configuration.FullFormat, year, month, day),
        DateStatus.DayUnknown => ToString(_Configuration.ShortFormat, year, month, day),
        DateStatus.MonthUnknown => year,
        DateStatus.YearApproximate => string.Format(UIStrings.DateStatusYearApproximate_1, year),
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
    var ret = date.Year.ToString(D4);

    return ret;
  }

  protected string MonthToString(Date date)
  {
    string ret;
    var month = date.Month;
    if (_Configuration.MonthAsNumber)
    {
      ret = month.ToString(D2);
    }
    else if (Language.Current == Language.RU)
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

  protected static string ToString(string format, string year, string month, string day)
  {
    var ret = format
      .Replace("YYYY", year)
      .Replace("MM", month)
      .Replace("DD", day);

    return ret;
  }
}