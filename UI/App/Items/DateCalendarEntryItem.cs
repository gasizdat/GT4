using GT4.Core.Project.Dto;
using GT4.UI.Utils;

namespace GT4.UI.Items;

public record class DateCalendarEntryItem(string Text, DateCalendarEventType Type, bool IsMilestone, PersonInfo Person);

public record class DateCalendarDayItem(int Day, bool IsToday, DateCalendarEntryItem[] Entries);
