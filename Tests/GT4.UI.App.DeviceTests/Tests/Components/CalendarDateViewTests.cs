using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Components;
using GT4.UI.Resources;
using Xunit;

namespace GT4.UI.DeviceTests;

public class CalendarDateViewTests
{
  private static async Task<TestableCalendarDateView> CreateViewAsync(DisplayCalendar calendar, string year, int monthIndex = 0, int dayIndex = 0)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var services = new TestServices();
    return await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var view = new TestableCalendarDateView(services.Provider);
      view.Calendar = calendar;
      view.Year = year;
      view.MonthIndex = monthIndex;
      view.DayIndex = dayIndex;
      return view;
    });
  }

  private static Date WellKnown(int year, int month, int day) => Date.Create(year, month, day, DateStatus.WellKnown);

  private static async Task<Picker[]> WaitForPickersAsync(CalendarDateView view)
  {
    var pickers = await MainThread.InvokeOnMainThreadAsync(() => view.GetVisualTreeDescendants().OfType<Picker>().ToArray());
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => pickers.All(p => p.IsLoaded)),
      loaded => loaded,
      timeoutMessage: "The pickers never loaded.");
    return pickers;
  }

  [Fact]
  public async Task The_entered_parts_give_their_Gregorian_date()
  {
    var view = await CreateViewAsync(DisplayCalendar.Julian, "1861", monthIndex: 1, dayIndex: 18);

    var expected = WellKnown(1861, 3, 3);
    Assert.Equal(expected, view.Date);
  }

  [Fact]
  public async Task Setting_Date_shows_it_in_the_views_calendar()
  {
    var view = await CreateViewAsync(DisplayCalendar.Julian, "2000");

    await MainThread.InvokeOnMainThreadAsync(() => view.Date = WellKnown(1861, 3, 3));

    Assert.Equal("1861", view.Year);
    Assert.Equal(1, view.MonthIndex);
    Assert.Equal(18, view.DayIndex);
  }

  [Fact]
  public async Task Setting_no_Date_clears_the_entry()
  {
    var view = await CreateViewAsync(DisplayCalendar.Julian, "1861", monthIndex: 1, dayIndex: 18);

    await MainThread.InvokeOnMainThreadAsync(() => view.Date = null);

    Assert.Equal("0", view.Year);
    Assert.Equal(0, view.MonthIndex);
    Assert.Equal(0, view.DayIndex);
    Assert.Null(view.Date);
  }

  [Fact]
  public async Task A_year_typed_into_a_cleared_Hebrew_entry_gives_its_date()
  {
    var view = await CreateViewAsync(DisplayCalendar.Hebrew, "5784", monthIndex: 7);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.Date = null;
      view.Year = "5785";
    });

    var expected = CalendarConversion.ToGregorian(DisplayCalendar.Hebrew, 5785, 1, 1);
    Assert.Equal(expected, view.Date);
  }

  [Theory]
  [InlineData(DisplayCalendar.Julian, 29)]
  [InlineData(DisplayCalendar.Gregorian, 28)]
  public async Task February_1900_has_the_calendars_own_day_count(DisplayCalendar calendar, int days)
  {
    var view = await CreateViewAsync(calendar, "1900", monthIndex: 1);

    Assert.Equal(days, view.Days.Length);
  }

  [Fact]
  public async Task A_month_change_clamps_the_day_to_the_new_month()
  {
    var view = await CreateViewAsync(DisplayCalendar.Gregorian, "2001", monthIndex: 0, dayIndex: 30);

    await MainThread.InvokeOnMainThreadAsync(() => view.MonthIndex = 1);

    Assert.Equal(27, view.DayIndex);
    var expected = WellKnown(2001, 2, 28);
    Assert.Equal(expected, view.Date);
  }

  [Fact]
  public async Task A_calendar_change_reinterprets_the_entered_numbers()
  {
    var view = await CreateViewAsync(DisplayCalendar.Gregorian, "1900", monthIndex: 1, dayIndex: 27);

    await MainThread.InvokeOnMainThreadAsync(() => view.Calendar = DisplayCalendar.Julian);

    Assert.Equal("1900", view.Year);
    Assert.Equal(1, view.MonthIndex);
    Assert.Equal(27, view.DayIndex);
    var expected = CalendarConversion.ToGregorian(DisplayCalendar.Julian, 1900, 2, 28);
    Assert.Equal(expected, view.Date);
  }

  // Typing 5784 passes through 5, 57 and 578, none of which the Hebrew lookups accept.
  [Theory]
  [InlineData("5")]
  [InlineData("5342")]
  [InlineData("6000")]
  [InlineData("")]
  public async Task A_year_outside_the_calendars_range_has_no_date(string year)
  {
    var view = await CreateViewAsync(DisplayCalendar.Hebrew, year);

    Assert.Null(view.Date);
    Assert.NotEmpty(view.Months);
    Assert.NotEmpty(view.Days);
  }

  // 5784 is a leap year and 5785 a common one: Nisan moves from month 8 to month 7.
  [Fact]
  public async Task A_Hebrew_year_change_keeps_the_month_by_name()
  {
    var view = await CreateViewAsync(DisplayCalendar.Hebrew, "5784", monthIndex: 7);

    await MainThread.InvokeOnMainThreadAsync(() => view.Year = "5785");

    Assert.Equal(6, view.MonthIndex);
    Assert.EndsWith(UIStrings.MonthHebrew_Nisan, view.Months[view.MonthIndex]);
  }

  // Retyping 5784 as 5787, both leap years, passes through 578, outside the range.
  [Fact]
  public async Task Typing_a_year_through_an_out_of_range_one_keeps_the_month()
  {
    var view = await CreateViewAsync(DisplayCalendar.Hebrew, "5784", monthIndex: 5);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.Year = "578";
      view.Year = "5787";
    });

    Assert.Equal(5, view.MonthIndex);
    Assert.EndsWith(UIStrings.MonthHebrew_AdarI, view.Months[view.MonthIndex]);
  }

  // Retyping 2000 as 2004 passes through 200, whose February has 28 days.
  [Fact]
  public async Task Typing_a_year_through_a_shorter_month_keeps_the_day()
  {
    var view = await CreateViewAsync(DisplayCalendar.Gregorian, "2000", monthIndex: 1, dayIndex: 28);
    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = view });
    await using var window = await WindowHost.AttachAsync(page);
    await WaitForPickersAsync(view);

    await MainThread.InvokeOnMainThreadAsync(() => view.Year = "200");
    Assert.Equal(27, view.DayIndex);
    await MainThread.InvokeOnMainThreadAsync(() => view.Year = "2004");

    Assert.Equal(28, view.DayIndex);
    var expected = WellKnown(2004, 2, 29);
    Assert.Equal(expected, view.Date);
  }

  // Attached, the native pickers have their lists replaced under a live selection.
  [Fact]
  public async Task Replacing_the_month_list_keeps_the_selection_on_screen()
  {
    var view = await CreateViewAsync(DisplayCalendar.Hebrew, "5784", monthIndex: 12, dayIndex: 28);
    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = view });
    await using var window = await WindowHost.AttachAsync(page);
    var pickers = await WaitForPickersAsync(view);

    await MainThread.InvokeOnMainThreadAsync(() => view.Year = "5785");

    var selected = await MainThread.InvokeOnMainThreadAsync(() => pickers.Select(p => p.SelectedIndex).ToArray());
    Assert.Equal([2, 11, 28], selected);
    var expected = CalendarConversion.ToGregorian(DisplayCalendar.Hebrew, 5785, 12, 29);
    Assert.Equal(expected, view.Date);
  }
}
