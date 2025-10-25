using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI;

public class DateFormatter : IDateFormatter
{
  public string ToString(DateOnly? date, DateStatus dateStatus)
  {
    // TODO use configuration

    if (date.HasValue)
    {
      switch (dateStatus)
      {
        case DateStatus.WellKnown:
          return date.Value.ToString("DD MMM yyyy");
        case DateStatus.DayUnknown:
          return date.Value.ToString("MMM yyyy");
        case DateStatus.MonthUnknown:
          return date.Value.ToString("yyyy");
        case DateStatus.YearApproximate:
          return string.Format(UIStrings.DateStatusYearApproximate_1, date.Value.ToString("yyyy"));
      }
    }

    return UIStrings.DateStatusUnknown;
  }
}