using GT4.Core.Project.Dto;
using GT4.Core.Utils;
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
  public string ToYearsString(Person person)
  {
    var birthYear = Year(person.BirthDate);
    if (person.DeathDate is not Date deathDate)
    {
      return birthYear;
    }

    var deathMark = string.Format(UIStrings.PersonDeathMark_1, Year(deathDate));
    return birthYear == string.Empty ? deathMark : string.Format(UIStrings.PersonDates_2, birthYear, deathMark);
  }

  private static string Year(Date date) => date.Status switch
  {
    DateStatus.Unknown => string.Empty,
    DateStatus.YearApproximate => $"~{DateFormatter.GregorianYears(date)}",
    _ => DateFormatter.GregorianYears(date),
  };
}
