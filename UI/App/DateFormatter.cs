using GT4.Core.Project.Dto;

namespace GT4.UI;

public class DateFormatter : IDateFormatter
{
  public string ToString(DateOnly? date, DateStatus dateStatus)
  {
    if (date.HasValue)
    {
      // TODO use configuration

      return date.Value.ToString();
    }

    return "";
  }
}