using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.Utils.Formatters;

internal class BirthCohortFormatter : IBirthCohortFormatter
{
  // An Unknown date carries year 0, so it falls below the table.
  public string? ToString(Date birthDate) =>
    (birthDate.Sign * birthDate.Year) switch
    {
      < 1883 => null,
      <= 1900 => UIStrings.BirthCohortLost,
      <= 1927 => UIStrings.BirthCohortGreatest,
      <= 1945 => UIStrings.BirthCohortSilent,
      <= 1964 => UIStrings.BirthCohortBabyBoomers,
      <= 1980 => UIStrings.BirthCohortX,
      <= 1996 => UIStrings.BirthCohortMillennials,
      <= 2012 => UIStrings.BirthCohortZ,
      <= 2024 => UIStrings.BirthCohortAlpha,
      _ => UIStrings.BirthCohortBeta
    };
}
