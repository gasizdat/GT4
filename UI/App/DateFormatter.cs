using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI;

public class DateFormatter : IDateFormatter
{
  private const string D4 = "D4";
  private const string D2 = "D2";

  public string ToString(Date? date)
  {
    // TODO Use configuration

    // TODO Apply Date.Sign

    if (date.HasValue)
    {
      switch (date.Value.Status)
      {
        case DateStatus.WellKnown:
          return $"{date.Value.Year.ToString(D4)}-{date.Value.Month.ToString(D2)}-{date.Value.Day.ToString(D2)}";
        case DateStatus.DayUnknown:
          return $"{date.Value.Year.ToString(D4)}-{date.Value.Month.ToString(D2)}";
        case DateStatus.MonthUnknown:
          return date.Value.Year.ToString(D4);
        case DateStatus.YearApproximate:
          return string.Format(UIStrings.DateStatusYearApproximate_1, date.Value.Year.ToString(D4));
        case DateStatus.Unknown:
          return UIStrings.DateStatusUnknown;
        default:
          return $"⚠ Unexpected DateStatus={date.Value.Status}";
      }
    }
    else
    {
      return UIStrings.DateStatusNotDefined;
    }
  }
}