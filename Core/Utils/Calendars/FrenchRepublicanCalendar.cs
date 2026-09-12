namespace GT4.Core.Utils.Calendars;

/// <summary>
/// Gregorian &lt;-&gt; French Republican conversion, valid only for Republican years I-XIV -- the only
/// years the calendar was ever in official use (22 September 1792 to 1 January 1806). Months 1-12
/// always run 30 days regardless of a sextile year, so only the epoch needs a leap-aware lookup; the
/// 5-6 complementary days (month 13) are unmodeled, so a date landing there is reported as not
/// representable rather than approximated.
/// </summary>
public static class FrenchRepublicanCalendar
{
  private const int DaysPerMonth = 30;
  private const int MonthsPerYear = 12;

  // Shared with GedcomDate's parser as the literal tokens GEDCOM text uses -- not a display string,
  // so localizing it would break import of French Republican dates.
  public static readonly string[] MonthAbbreviations =
    ["VEND", "BRUM", "FRIM", "NIVO", "PLUV", "VENT", "GERM", "FLOR", "PRAI", "MESS", "THER", "FRUC"];

  // The display sibling of MonthAbbreviations above, kept as one French spelling for every UI
  // language rather than localized per calendar setting.
  public static readonly string[] MonthNames =
  [
    "Vendémiaire", "Brumaire", "Frimaire", "Nivôse", "Pluviôse", "Ventôse",
    "Germinal", "Floréal", "Prairial", "Messidor", "Thermidor", "Fructidor",
  ];

  // 1 Vendemiaire of Republican years I-XIV in the Gregorian calendar.
  private static readonly (int Year, int Month, int Day)[] Epochs =
  [
    (1792, 9, 22), (1793, 9, 22), (1794, 9, 22), (1795, 9, 23), (1796, 9, 22), (1797, 9, 22), (1798, 9, 22),
    (1799, 9, 23), (1800, 9, 23), (1801, 9, 23), (1802, 9, 23), (1803, 9, 24), (1804, 9, 23), (1805, 9, 23),
  ];

  // The calendar's abolition, not another epoch -- year XIV never completed a full cycle, so nothing
  // bounds a naive epoch-window lookup from above without this.
  private static readonly DateTime Abolition = new(1806, 1, 1);

  public static DateTime? ToGregorian(int year, int month, int day)
  {
    if (day is < 1 or > DaysPerMonth || month is < 1 or > MonthsPerYear || year < 1 || year > Epochs.Length)
      return null;

    var epoch = EpochStart(year);
    var gregorian = epoch.AddDays((month - 1) * DaysPerMonth + day - 1);
    return gregorian < Abolition ? gregorian : null;
  }

  public static bool TryFromGregorian(DateTime gregorian, out (int Year, int Month, int Day) republican)
  {
    republican = default;
    if (gregorian < EpochStart(1) || gregorian >= Abolition)
      return false;

    var year = Epochs.Length;
    while (year > 1 && gregorian < EpochStart(year))
      year--;

    var offset = (gregorian - EpochStart(year)).Days;
    if (offset >= MonthsPerYear * DaysPerMonth)
      return false; // complementary days -- unmodeled

    republican = (year, offset / DaysPerMonth + 1, offset % DaysPerMonth + 1);
    return true;
  }

  private static DateTime EpochStart(int year)
  {
    var epoch = Epochs[year - 1];
    return new DateTime(epoch.Year, epoch.Month, epoch.Day);
  }
}
