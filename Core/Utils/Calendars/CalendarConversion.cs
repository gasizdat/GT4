using System.Globalization;

namespace GT4.Core.Utils.Calendars;

/// <summary>
/// Converts between a stored (Gregorian) <see cref="Date"/> and another calendar, storage
/// itself stays Gregorian-only. Each non-Gregorian calendar has a real range outside which it
/// cannot represent a date -- Hebrew's year 5343-5999 AM (<see cref="HebrewCalendar"/>'s own
/// range), French Republican's years I-XIV, Julian's only bound is <see cref="DateTime"/>'s own
/// year 1-9999 ceiling. These ranges begin and end mid-year (Hebrew starts on 7 Tevet 5343).
/// Outside that range, <see cref="TryConvert"/> falls back to the original Gregorian value and
/// reports <see cref="DisplayCalendar.Gregorian"/> as what was actually applied, and
/// <see cref="ToGregorian"/> returns <c>null</c>, rather than throwing or approximating.
/// </summary>
public static class CalendarConversion
{
  // Adar I in a leap year shares Adar's number.
  private const int HebrewAdar = 6;
  private const int HebrewAdarII = 7;

  private static readonly GregorianCalendar Gregorian = new();
  private static readonly JulianCalendar Julian = new();
  private static readonly HebrewCalendar Hebrew = new();

  /// <summary>A hand-edited config can name a calendar that is not one of the four offered.</summary>
  public static DisplayCalendar ToDisplayCalendar(string? calendarType) =>
    Enum.TryParse<DisplayCalendar>(calendarType, out var parsed) ? parsed : DisplayCalendar.Gregorian;

  /// <summary>Assumes <paramref name="date"/> is <see cref="DateStatus.WellKnown"/> -- a partial date
  /// (year/month only) has no single correct placement in another calendar, so callers convert only
  /// full dates and render every other status in Gregorian.</summary>
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

  /// <summary><c>null</c> when the date does not exist in <paramref name="calendar"/> or falls outside
  /// the range it can represent.</summary>
  public static Date? ToGregorian(DisplayCalendar calendar, int year, int month, int day)
  {
    if (calendar == DisplayCalendar.FrenchRepublican)
    {
      var republican = FrenchRepublicanCalendar.ToGregorian(year, month, day);
      return republican is null ? null : Date.Create(republican.Value);
    }

    // Range first: the month and day lookups throw for a year outside it.
    var system = SystemCalendar(calendar);
    var first = DateOf(system, system.MinSupportedDateTime);
    var last = DateOf(system, system.MaxSupportedDateTime);
    var date = (year, month, day);
    if (date.CompareTo(first) < 0 || date.CompareTo(last) > 0 ||
        month < 1 || month > system.GetMonthsInYear(year) ||
        day < 1 || day > system.GetDaysInMonth(year, month))
      return null;

    var gregorian = system.ToDateTime(year, month, day, 0, 0, 0, 0);
    return Date.Create(gregorian);
  }

  /// <summary>The years <see cref="MonthsInYear"/> and <see cref="DaysInMonth"/> accept. The first and
  /// last of them may be only partly representable.</summary>
  public static (int Min, int Max) YearRange(DisplayCalendar calendar)
  {
    if (calendar == DisplayCalendar.FrenchRepublican)
      return (1, FrenchRepublicanCalendar.LastYear);

    var system = SystemCalendar(calendar);
    var first = system.GetYear(system.MinSupportedDateTime);
    var last = system.GetYear(system.MaxSupportedDateTime);
    return (first, last);
  }

  public static int MonthsInYear(DisplayCalendar calendar, int year) =>
    calendar == DisplayCalendar.FrenchRepublican
      ? FrenchRepublicanCalendar.MonthsPerYear
      : SystemCalendar(calendar).GetMonthsInYear(year);

  public static int DaysInMonth(DisplayCalendar calendar, int year, int month) =>
    calendar == DisplayCalendar.FrenchRepublican
      ? FrenchRepublicanCalendar.DaysPerMonth
      : SystemCalendar(calendar).GetDaysInMonth(year, month);

  /// <summary>Whether <paramref name="year"/> is a 13-month Hebrew leap year -- Adar splits into Adar I
  /// (month 6) and Adar II (month 7), shifting every later month's number up by one.</summary>
  public static bool IsHebrewLeapYear(int year) => Hebrew.IsLeapYear(year);

  /// <summary>The number <paramref name="month"/> of <paramref name="fromYear"/> has in
  /// <paramref name="toYear"/>, kept by name. Adar maps to Adar II, as the leap year's Adar II holds the
  /// observances of a common year's Adar.</summary>
  public static int HebrewMonthInYear(int month, int fromYear, int toYear) =>
    (IsHebrewLeapYear(fromYear), IsHebrewLeapYear(toYear)) switch
    {
      (true, false) when month >= HebrewAdarII => month - 1,
      (false, true) when month >= HebrewAdar => month + 1,
      _ => month,
    };

  private static Calendar SystemCalendar(DisplayCalendar calendar) => calendar switch
  {
    DisplayCalendar.Gregorian => Gregorian,
    DisplayCalendar.Julian => Julian,
    DisplayCalendar.Hebrew => Hebrew,
    _ => throw new NotImplementedException($"DisplayCalendar={calendar}")
  };

  private static (int Year, int Month, int Day) DateOf(Calendar calendar, DateTime time) =>
    (calendar.GetYear(time), calendar.GetMonth(time), calendar.GetDayOfMonth(time));
}
