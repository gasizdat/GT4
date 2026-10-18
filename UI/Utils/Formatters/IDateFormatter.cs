using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;

namespace GT4.UI.Utils.Formatters;

public interface IDateFormatter
{
  DisplayCalendar Calendar { get; }

  string ToString(Date? date);

  string ToString(Date? date, DisplayCalendar requestedCalendar);

  string CalendarName(DisplayCalendar calendar);

  /// <summary>Nominative, as a picker lists them. <paramref name="year"/> must be within
  /// <see cref="CalendarConversion.YearRange"/>: a Hebrew year decides whether Adar is split.</summary>
  string[] MonthNames(DisplayCalendar calendar, int year);

  string CalendarRange(DisplayCalendar calendar);
}
