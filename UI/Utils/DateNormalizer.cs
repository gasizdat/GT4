namespace GT4.UI.Utils;

public static class DateNormalizer
{
  public static int NormalizeMonth(int month) => Math.Clamp(month, 1, 12);

  public static bool IsLeapYear(int year) =>
    (year % 4) == 0 && ((year % 100) != 0 || (year % 400) == 0);

  public static int NormalizeDay(int year, int month, int day)
  {
    var daysInMonth = month switch
    {
      1 => 31,
      2 => IsLeapYear(year) ? 29 : 28,
      3 => 31,
      4 => 30,
      5 => 31,
      6 => 30,
      7 => 31,
      8 => 31,
      9 => 30,
      10 => 31,
      11 => 30,
      12 => 31,
      _ => 0
    };
    return Math.Clamp(day, 1, daysInMonth);
  }
}
