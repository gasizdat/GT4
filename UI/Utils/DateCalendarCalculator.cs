using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI.Utils;

public enum DateCalendarEventType
{
  Birth,
  Death,
  Wedding,
}

public record class DateCalendarEntry(DateCalendarEventType Type, PersonInfo Person, PersonInfo? Spouse, int Years, bool IsMilestone);

public record class DateCalendarDay(int Day, DateCalendarEntry[] Entries);

public record class DateCalendarMonth(DateCalendarDay[] Days, DateCalendarEntry[] DayUnknown);

/// <summary>
/// Pure computation of a month's birth/death/wedding entries over a project's persons/relatives, owned
/// and driven by <c>DateCalendarPage</c> (GT4.UI.App). Mirrors the <see cref="ProjectStatisticsCalculator"/>
/// precedent: no DB access here, the page fetches the inputs itself and passes them in.
/// </summary>
public static class DateCalendarCalculator
{
  private const int MilestoneInterval = 25;

  public static DateCalendarMonth Compute(
    PersonInfo[] persons, IReadOnlyDictionary<int, Relative[]> relativesByPersonId, int month, int currentYear)
  {
    var personsById = persons.ToDictionary(p => p.Id);

    var births = persons.SelectMany(p => Entry(DateCalendarEventType.Birth, p, null, p.BirthDate, month, currentYear));
    var deaths = persons.SelectMany(p => p.DeathDate is { } deathDate
      ? Entry(DateCalendarEventType.Death, p, null, deathDate, month, currentYear)
      : []);
    var weddings = Weddings(persons, relativesByPersonId, personsById, month, currentYear);

    var entries = births.Concat(deaths).Concat(weddings).ToArray();

    var days = entries
      .Where(e => e.Date.Status == DateStatus.WellKnown)
      .GroupBy(e => e.Date.Day)
      .Select(g => new DateCalendarDay(g.Key, [.. g.Select(e => e.Entry)]))
      .OrderBy(d => d.Day)
      .ToArray();

    var dayUnknown = entries
      .Where(e => e.Date.Status == DateStatus.DayUnknown)
      .Select(e => e.Entry)
      .ToArray();

    return new DateCalendarMonth(days, dayUnknown);
  }

  public static int CountUnplaceable(PersonInfo[] persons, IReadOnlyDictionary<int, Relative[]> relativesByPersonId)
  {
    bool IsUnplaceable(Date date) => date.Status is DateStatus.MonthUnknown or DateStatus.YearApproximate;

    return persons.Count(p => IsUnplaceable(p.BirthDate) || (p.DeathDate is { } d && IsUnplaceable(d)));
  }

  // Date.SignedYear (1 B.C. = astronomical year 0, so there is no year 0) is private; replicate it here
  // since it's the only correct way to turn a Date into a year for arithmetic.
  private static int SignedYear(Date date) => date.Sign < 0 ? 1 - date.Year : date.Year;

  private static IEnumerable<(DateCalendarEntry Entry, Date Date)> Entry(
    DateCalendarEventType type, PersonInfo person, PersonInfo? spouse, Date date, int month, int currentYear)
  {
    if (date.Status >= DateStatus.MonthUnknown || date.Month != month)
      yield break;

    var years = currentYear - SignedYear(date);
    var isMilestone = years > 0 && years % MilestoneInterval == 0;
    yield return (new DateCalendarEntry(type, person, spouse, years, isMilestone), date);
  }

  private static IEnumerable<(DateCalendarEntry Entry, Date Date)> Weddings(
    PersonInfo[] persons,
    IReadOnlyDictionary<int, Relative[]> relativesByPersonId,
    IReadOnlyDictionary<int, PersonInfo> personsById,
    int month,
    int currentYear)
  {
    var weddingDatesByPair = new Dictionary<(int, int), Date?>();
    foreach (var person in persons)
    {
      if (!relativesByPersonId.TryGetValue(person.Id, out var relatives))
        continue;

      foreach (var spouse in relatives.Where(r => r.Type == RelationshipType.Spouse))
      {
        var pair = (Math.Min(person.Id, spouse.Id), Math.Max(person.Id, spouse.Id));
        if (!weddingDatesByPair.ContainsKey(pair))
        {
          weddingDatesByPair[pair] = spouse.Date;
        }
      }
    }

    return weddingDatesByPair
      .Where(pair => pair.Value is not null)
      .SelectMany(pair => Entry(
        DateCalendarEventType.Wedding, personsById[pair.Key.Item1], personsById[pair.Key.Item2], pair.Value!.Value, month, currentYear));
  }
}
