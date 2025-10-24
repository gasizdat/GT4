using GT4.Core.Project.Dto;

namespace GT4.UI;

public interface IDateFormatter
{
  string ToString(DateOnly? date, DateStatus dateStatus);
}