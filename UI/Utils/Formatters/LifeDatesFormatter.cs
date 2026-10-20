using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Resources;

namespace GT4.UI.Utils.Formatters;

internal class LifeDatesFormatter : ILifeDatesFormatter
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IDateSpanFormatter _DateSpanFormatter;

  public LifeDatesFormatter(IDateFormatter dateFormatter, IDateSpanFormatter dateSpanFormatter)
  {
    _DateFormatter = dateFormatter;
    _DateSpanFormatter = dateSpanFormatter;
  }

  public string ToString(Person person, bool showDeathDate, bool showAge)
  {
    var personDates = string.Empty;
    var isDeathDateDisplayed = showDeathDate && person.DeathDate.HasValue;

    if (person.BirthDate.Status != DateStatus.Unknown || !isDeathDateDisplayed)
    {
      personDates = _DateFormatter.ToString(person.BirthDate);
    }

    if (isDeathDateDisplayed)
    {
      var deathDate = person.DeathDate!.Value.Status == DateStatus.Unknown
                      ? string.Empty
                      : _DateFormatter.ToString(person.DeathDate);
      deathDate = string.Format(UIStrings.PersonDeathMark_1, deathDate);

      if (personDates == string.Empty)
      {
        personDates = deathDate;
      }
      else
      {
        personDates = string.Format(UIStrings.PersonDates_2, personDates, deathDate);
      }
    }

    if (showAge)
    {
      var timeSpan = (person.DeathDate.HasValue ? person.DeathDate : Date.Now) - person.BirthDate;
      if (timeSpan.HasValue && timeSpan.Value.Status != DateStatus.Unknown)
      {
        personDates = string.Format(UIStrings.PersonAge_2, personDates, _DateSpanFormatter.ToString(timeSpan));
      }
    }

    return personDates;
  }

  // For a space too small for whole dates, so an approximate year takes a mark, not "about".
  public (string Text, bool IsFallback)[] ToYears(Person person, DisplayCalendar calendar)
  {
    var birthYear = Year(person.BirthDate, calendar);
    if (person.DeathDate is not Date deathDate)
    {
      return birthYear;
    }

    var deathYear = Year(deathDate, calendar);
    var deathMark = Compose(UIStrings.PersonDeathMark_1, deathYear);
    return birthYear.Length == 0 ? deathMark : Compose(UIStrings.PersonDates_2, birthYear, deathMark);
  }

  // No template here holds a literal brace, so every odd part is a placeholder's index.
  private static (string Text, bool IsFallback)[] Compose(string format, params (string Text, bool IsFallback)[][] args)
  {
    var parts = format.Split(['{', '}']);
    var runs = parts.SelectMany((part, i) => i % 2 == 0 ? [(part, false)] : args[int.Parse(part)]);
    return [.. runs.Where(run => run.Text.Length != 0)];
  }

  private static (string Text, bool IsFallback)[] Year(Date date, DisplayCalendar calendar)
  {
    if (date.Status == DateStatus.Unknown)
    {
      return [];
    }

    var (applied, year, approximate) = ConvertYear(date, calendar);
    var text = DateFormatter.GregorianYears(date with { Year = year });
    return [(approximate ? $"~{text}" : text, applied != calendar)];
  }

  private static (DisplayCalendar Applied, int Year, bool Approximate) ConvertYear(Date date, DisplayCalendar calendar)
  {
    if (calendar == DisplayCalendar.Gregorian)
    {
      return (calendar, date.Year, date.Status == DateStatus.YearApproximate);
    }

    if (date.Status == DateStatus.WellKnown)
    {
      var (applied, year, _, _) = CalendarConversion.TryConvert(date, calendar);
      return (applied, year, false);
    }

    return CalendarConversion.TryConvertYear(date, calendar);
  }
}
