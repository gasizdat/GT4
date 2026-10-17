using GT4.Core.Utils;

namespace GT4.UI.Utils;

public static class DateNormalizer
{
  public static int NormalizeMonth(int month) => Math.Clamp(month, 1, 12);

  public static int NormalizeDay(int year, int month, int day)
  {
    var daysInMonth = Date.DaysInMonth(year, month);
    return Math.Clamp(day, 1, daysInMonth);
  }
}
