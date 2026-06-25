using GT4.Core.Utils;

namespace GT4.Core.Gedcom;

/// <summary>
/// Converts between the GT4 <see cref="Date"/> (a packed year/month/day with a certainty
/// <see cref="DateStatus"/>) and the GEDCOM 5.5.1 date string. The mapping is intentionally lossy in the
/// directions GEDCOM cannot express: any "before/after/between" qualifier collapses to an approximate
/// year, matching GT4's single <see cref="DateStatus.YearApproximate"/> notion.
/// </summary>
internal static class GedcomDate
{
  private static readonly string[] Months =
    ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

  private const string BeforeChrist = "B.C.";

  /// <summary>The GEDCOM rendering of <paramref name="date"/>, or <c>null</c> when nothing is known.</summary>
  public static string? ToGedcom(Date date)
  {
    if (date.Status == DateStatus.Unknown)
      return null;

    var body = date.Status switch
    {
      DateStatus.WellKnown => $"{date.Day} {Months[date.Month - 1]} {date.Year}",
      DateStatus.DayUnknown => $"{Months[date.Month - 1]} {date.Year}",
      DateStatus.MonthUnknown => $"{date.Year}",
      DateStatus.YearApproximate => $"ABT {date.Year}",
      _ => $"{date.Year}",
    };

    return date.Sign < 0 ? $"{body} {BeforeChrist}" : body;
  }

  /// <summary>Parses a GEDCOM date, returning an <see cref="DateStatus.Unknown"/> date when it cannot.</summary>
  public static Date Parse(string? value)
  {
    var unknown = new Date { Status = DateStatus.Unknown };
    if (string.IsNullOrWhiteSpace(value))
      return unknown;

    var tokens = value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var beforeChrist = RemoveBeforeChrist(ref tokens);
    var approximate = RemoveQualifiers(ref tokens);

    var parsed = ParseComponents(tokens);
    if (parsed is null)
      return unknown;

    var (year, month, day) = parsed.Value;
    var status = approximate
      ? DateStatus.YearApproximate
      : month is null ? DateStatus.MonthUnknown
      : day is null ? DateStatus.DayUnknown
      : DateStatus.WellKnown;

    var code = year * 10_000 + (month ?? 0) * 100 + (day ?? 0);
    return Date.Create(beforeChrist ? -code : code, status);
  }

  private static bool RemoveBeforeChrist(ref string[] tokens)
  {
    var beforeChrist = tokens.Any(t => t is BeforeChrist or "BC");
    if (beforeChrist)
    {
      tokens = tokens.Where(t => t is not (BeforeChrist or "BC")).ToArray();
    }
    return beforeChrist;
  }

  /// <summary>
  /// Strips range/approximation qualifiers. Anything other than an exact date (ABT/EST/CAL, but also
  /// BEF/AFT and a BET..AND range, of which only the first endpoint is kept) is treated as an
  /// approximate year, since that is the only inexact notion GT4 stores.
  /// </summary>
  private static bool RemoveQualifiers(ref string[] tokens)
  {
    var qualifiers = new HashSet<string> { "ABT", "EST", "CAL", "BEF", "AFT", "FROM", "TO", "BET" };
    var approximate = false;
    var kept = new List<string>();
    foreach (var token in tokens)
    {
      if (token == "AND")
        break; // Keep only the first endpoint of a BET x AND y range.
      if (qualifiers.Contains(token))
      {
        approximate = true;
        continue;
      }
      kept.Add(token);
    }
    tokens = kept.ToArray();
    return approximate;
  }

  private static (int Year, int? Month, int? Day)? ParseComponents(string[] tokens)
  {
    switch (tokens.Length)
    {
      case 1:
        return int.TryParse(tokens[0], out var yearOnly) ? (yearOnly, null, null) : null;
      case 2:
      {
        var month = MonthNumber(tokens[0]);
        return month is not null && int.TryParse(tokens[1], out var year) ? (year, month, null) : null;
      }
      case 3:
      {
        var month = MonthNumber(tokens[1]);
        var hasDay = int.TryParse(tokens[0], out var day);
        var hasYear = int.TryParse(tokens[2], out var year);
        return month is not null && hasDay && hasYear ? (year, month, day) : null;
      }
      default:
        return null;
    }
  }

  private static int? MonthNumber(string token)
  {
    var index = Array.IndexOf(Months, token);
    return index < 0 ? null : index + 1;
  }
}
