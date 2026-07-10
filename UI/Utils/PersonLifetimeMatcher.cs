using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI.Utils;

public static class PersonLifetimeMatcher
{
  private const int UnknownDateGuessYears = 20;
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

    var start = birthKnown ? birthDate.Year : deathDate!.Value.Year - UnknownDateGuessYears;
    var end = deathYearKnown
      ? deathDate!.Value.Year
      : deathDate is null
        ? start + MaximumLifeExpectancyYears
        : birthDate.Year + UnknownDateGuessYears;

    return year >= start && year <= end;
  }

  public static bool IsAliveInYear(PersonInfo person, int year) =>
    IsAliveInYear(person.BirthDate, person.DeathDate, year);
}
