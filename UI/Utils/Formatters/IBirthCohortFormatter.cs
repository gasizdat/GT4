using GT4.Core.Utils;

namespace GT4.UI.Utils.Formatters;

public interface IBirthCohortFormatter
{
  string? ToString(Date birthDate);
}
