using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Formatters;

public interface ILifeDatesFormatter
{
  string ToString(Person person, bool showDeathDate, bool showAge);
}
