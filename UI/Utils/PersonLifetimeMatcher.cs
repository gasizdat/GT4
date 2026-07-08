using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI.Utils;

public static class PersonLifetimeMatcher
{
  private const int MaximumLifeExpectancyYears = 120;

  // A person matches `year` if it falls within their [effective birth year, effective death year]
  // lifetime. If neither end can be pinned to a real recorded year (including "no dates at all" and
  // "alive, but birth is unknown"), the person has no known lifetime to test the year against, so
  // they never match while this filter is active.
  public static bool IsAliveInYear(Date birthDate, Date? deathDate, int year)
  {
    var birthKnown = birthDate.Status != DateStatus.Unknown;
    var deathYearKnown = deathDate is { Status: not DateStatus.Unknown };

    if (!birthKnown && !deathYearKnown)
    {
      return false;
    }

    // An unknown endpoint (birth or death) is completely unconstrained beyond "within a max lifespan
    // of the known endpoint" -- there's no more precise guess available, so both directions use the
    // same MaximumLifeExpectancyYears bound.
    var start = birthKnown ? birthDate.Year : deathDate!.Value.Year - MaximumLifeExpectancyYears;
    var end = deathYearKnown ? deathDate!.Value.Year : start + MaximumLifeExpectancyYears;

    return year >= start && year <= end;
  }

  public static bool IsAliveInYear(PersonInfo person, int year) =>
    IsAliveInYear(person.BirthDate, person.DeathDate, year);
}
