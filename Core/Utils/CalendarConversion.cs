using System.Globalization;

namespace GT4.Core.Utils;

public enum DisplayCalendar
{
  Gregorian,
  Julian,
  Hebrew,
  FrenchRepublican,
}

/// <summary>
/// Converts a stored (Gregorian) <see cref="Date"/> into another calendar for display, storage
/// itself stays Gregorian-only. Each non-Gregorian calendar has a real range outside which it
/// cannot represent a date -- Hebrew's year 5343-5999 AM (<see cref="HebrewCalendar"/>'s own
/// range), French Republican's years I-XIV, Julian's only bound is <see cref="DateTime"/>'s own
/// year 1-9999 ceiling. Outside that range, <see cref="TryConvert"/> falls back to the original
/// Gregorian value and reports <see cref="DisplayCalendar.Gregorian"/> as what was actually
/// applied, rather than throwing or approximating.
/// </summary>
public static class CalendarConversion
{
  private static readonly JulianCalendar Julian = new();
  private static readonly HebrewCalendar Hebrew = new();

  /// <summary>A hand-edited config can name a calendar that is not one of the four offered.</summary>
  public static DisplayCalendar Parse(string? calendar) =>
    Enum.TryParse<DisplayCalendar>(calendar, out var parsed) ? parsed : DisplayCalendar.Gregorian;

  public static (DisplayCalendar Applied, int Year, int Month, int Day) TryConvert(Date date, DisplayCalendar target)
  {
    var fallback = (Applied: DisplayCalendar.Gregorian, date.Year, date.Month, date.Day);
    if (target == DisplayCalendar.Gregorian || date.Sign < 0 || date.Year is < 1 or > 9999)
      return fallback;

    var gregorian = new DateTime(date.Year, date.Month, date.Day);
    return target switch
    {
      DisplayCalendar.Julian =>
        (DisplayCalendar.Julian, Julian.GetYear(gregorian), Julian.GetMonth(gregorian), Julian.GetDayOfMonth(gregorian)),
      DisplayCalendar.Hebrew when gregorian >= Hebrew.MinSupportedDateTime && gregorian <= Hebrew.MaxSupportedDateTime =>
        (DisplayCalendar.Hebrew, Hebrew.GetYear(gregorian), Hebrew.GetMonth(gregorian), Hebrew.GetDayOfMonth(gregorian)),
      DisplayCalendar.FrenchRepublican when FrenchRepublicanCalendar.TryFromGregorian(gregorian, out var republican) =>
        (DisplayCalendar.FrenchRepublican, republican.Year, republican.Month, republican.Day),
      _ => fallback,
    };
  }

  /// <summary>Whether <paramref name="year"/> is a 13-month Hebrew leap year -- Adar splits into Adar I
  /// (month 6) and Adar II (month 7), shifting every later month's number up by one.</summary>
  public static bool IsHebrewLeapYear(int year) => Hebrew.IsLeapYear(year);
}
