using GT4.Core.Project.Dto;
using GT4.Core.Utils.Calendars;

namespace GT4.UI.Utils.Formatters;

public interface ILifeDatesFormatter
{
  string ToString(Person person, bool showDeathDate, bool showAge);

  /// <summary><c>IsFallback</c> marks a year left in Gregorian because <paramref name="calendar"/> can't
  /// represent it.</summary>
  (string Text, bool IsFallback)[] ToYears(Person person, DisplayCalendar calendar);
}
