using GT4.Core.Utils;

namespace GT4.UI.Formatters;

public interface IDateFormatter
{
  string ToString(Date? date);
}