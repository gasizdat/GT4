using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SelectDateDialog directly: it has no IServiceProvider/navigation dependency at all, just
/// a real (unmocked) IDateFormatter and the mocked IAlertService behind its command, so it's
/// constructed straight from TestServices.
/// The switch cascade rules (YearSwitch/MonthSwitch/DaySwitch/ApproximateSwitch) are the page's own
/// non-obvious logic and the main thing worth pinning here.
/// </summary>
public class SelectDateDialogTests
{
  private static async Task<SelectDateDialog> CreateDialogAsync(TestServices services, Date? date)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dateFormatter = services.Provider.GetRequiredService<IDateFormatter>();
    return await MainThread.InvokeOnMainThreadAsync(() => new SelectDateDialog(date, dateFormatter, services.AlertService.Object));
  }

  private static Task<CalendarDateView> OtherCalendarViewAsync(SelectDateDialog dialog) =>
    MainThread.InvokeOnMainThreadAsync(() => dialog.GetVisualTreeDescendants().OfType<CalendarDateView>().Single());

  private static Date WellKnown(int year, int month, int day) => Date.Create(year, month, day, DateStatus.WellKnown);

  [Fact]
  public async Task Ctor_with_no_date_starts_empty_and_not_ready()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    Assert.Equal("0", dialog.Year);
    Assert.False(dialog.YearSwitch);
    Assert.False(dialog.MonthSwitch);
    Assert.False(dialog.DaySwitch);
    Assert.False(dialog.ApproximateSwitch);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Ctor_with_a_WellKnown_date_populates_fields_but_stays_not_ready_until_touched()
  {
    var date = Date.Create(2000, 5, 15, DateStatus.WellKnown);
    var dialog = await CreateDialogAsync(new TestServices(), date);

    // The ctor sets the backing fields directly (not through the properties), so Refresh() -- and
    // therefore "ready" -- is never reached just by constructing with an existing date; the user
    // must touch something first, same convention as CreateOrUpdateNameDialog/
    // CreateOrUpdatePersonDialog (edit an existing item still requires a change to enable Save).
    Assert.Equal("2000", dialog.Year);
    Assert.Equal(dialog.Months[4], dialog.Month);
    Assert.Equal(dialog.Days[14], dialog.Day);
    Assert.True(dialog.YearSwitch);
    Assert.True(dialog.MonthSwitch);
    Assert.True(dialog.DaySwitch);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Ctor_with_a_MonthUnknown_date_only_enables_the_year_switch()
  {
    var date = Date.Create(2000, 0, 0, DateStatus.MonthUnknown);
    var dialog = await CreateDialogAsync(new TestServices(), date);

    Assert.True(dialog.YearSwitch);
    Assert.False(dialog.MonthSwitch);
    Assert.False(dialog.DaySwitch);
  }

  [Fact]
  public async Task Ctor_with_a_DayUnknown_date_enables_year_and_month_but_not_day()
  {
    var date = Date.Create(2000, 5, 0, DateStatus.DayUnknown);
    var dialog = await CreateDialogAsync(new TestServices(), date);

    Assert.True(dialog.YearSwitch);
    Assert.True(dialog.MonthSwitch);
    Assert.False(dialog.DaySwitch);
  }

  [Fact]
  public async Task Ctor_with_a_BeforeCommonEra_date_shows_the_negative_year()
  {
    var date = Date.Create(-100, 0, 0, DateStatus.MonthUnknown);
    var dialog = await CreateDialogAsync(new TestServices(), date);

    Assert.Equal("-100", dialog.Year);
  }

  [Fact]
  public async Task Entering_a_negative_year_returns_a_BeforeCommonEra_date()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.Year = "-2");

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var result = await dialog.Info;

    Assert.Equal(Date.Create(-2, 1, 1, DateStatus.MonthUnknown), result);
  }

  [Fact]
  public async Task Changing_a_field_marks_the_dialog_ready()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.Year = "1990");

    Assert.Equal(Resources.UIStrings.BtnNameOk, dialog.DialogButtonName);
  }

  [Fact]
  public async Task Turning_off_YearSwitch_cascades_off_approximate_month_and_day()
  {
    var date = Date.Create(2000, 5, 15, DateStatus.WellKnown);
    var dialog = await CreateDialogAsync(new TestServices(), date);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.YearSwitch = false);

    Assert.False(dialog.ApproximateSwitch);
    Assert.False(dialog.MonthSwitch);
    Assert.False(dialog.DaySwitch);
  }

  [Fact]
  public async Task Turning_on_MonthSwitch_forces_YearSwitch_on()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.MonthSwitch = true);

    Assert.True(dialog.YearSwitch);
    Assert.False(dialog.ApproximateSwitch);
  }

  [Fact]
  public async Task Turning_on_DaySwitch_forces_year_and_month_switches_on()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DaySwitch = true);

    Assert.True(dialog.MonthSwitch);
    Assert.True(dialog.YearSwitch);
    Assert.False(dialog.ApproximateSwitch);
  }

  [Fact]
  public async Task Turning_on_ApproximateSwitch_forces_year_on_and_month_day_off()
  {
    var date = Date.Create(2000, 5, 15, DateStatus.WellKnown);
    var dialog = await CreateDialogAsync(new TestServices(), date);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.ApproximateSwitch = true);

    Assert.True(dialog.YearSwitch);
    Assert.False(dialog.MonthSwitch);
    Assert.False(dialog.DaySwitch);
  }

  [Fact]
  public async Task DialogCommand_returns_null_when_not_ready()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var result = await dialog.Info;

    Assert.Null(result);
  }

  [Fact]
  public async Task DialogCommand_returns_the_selected_date_once_touched()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.Year = "1990";
      dialog.MonthSwitch = true;
      dialog.Month = dialog.Months[2];
      dialog.DaySwitch = true;
      dialog.Day = dialog.Days[9];
    });

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var result = await dialog.Info;

    Assert.Equal(Date.Create(1990, 3, 10, DateStatus.WellKnown), result);
  }

  // The host's formatter reads the developer's own calendar setting, so a mock pins it.
  [Theory]
  [InlineData(DisplayCalendar.Gregorian, DisplayCalendar.Julian)]
  [InlineData(DisplayCalendar.Hebrew, DisplayCalendar.Hebrew)]
  public async Task The_other_calendar_is_the_display_calendar_unless_it_is_Gregorian(DisplayCalendar displayCalendar, DisplayCalendar expected)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var services = new TestServices();
    var dateFormatter = new Mock<IDateFormatter>();
    dateFormatter.SetupGet(f => f.Calendar).Returns(displayCalendar);
    var dialog = await MainThread.InvokeOnMainThreadAsync(() => new SelectDateDialog(null, dateFormatter.Object, services.AlertService.Object));
    var view = await OtherCalendarViewAsync(dialog);

    Assert.Equal(expected, dialog.OtherCalendar);
    Assert.Equal(expected, view.Calendar);
  }

  [Fact]
  public async Task A_date_entered_in_another_calendar_returns_its_Gregorian_date()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services, null);
    var view = await OtherCalendarViewAsync(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.OtherCalendarSwitch = true;
      view.Calendar = DisplayCalendar.Julian;
      view.Year = "1861";
      view.MonthIndex = 1;
      view.DayIndex = 18;
    });

    var expected = WellKnown(1861, 3, 3);
    var expectedText = services.Provider.GetRequiredService<IDateFormatter>().ToString(expected);
    Assert.Equal(expectedText, dialog.DateString);
    Assert.Equal(Resources.UIStrings.BtnNameOk, dialog.DialogButtonName);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    Assert.Equal(expected, await dialog.Info);
  }

  [Fact]
  public async Task A_date_outside_the_other_calendar_says_why_and_returns_nothing()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services, WellKnown(2000, 5, 15));
    var view = await OtherCalendarViewAsync(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.OtherCalendarSwitch = true;
      view.Calendar = DisplayCalendar.Hebrew;
      view.Year = "5342";
    });

    var name = services.Provider.GetRequiredService<IDateFormatter>().CalendarName(DisplayCalendar.Hebrew);
    var expectedText = string.Format(Resources.UIStrings.DateToolsInputNotRepresentable_2, name, Resources.UIStrings.DateToolsRangeHebrew);
    Assert.Equal(expectedText, dialog.DateString);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    Assert.Null(await dialog.Info);
  }

  [Fact]
  public async Task Turning_the_other_calendar_on_shows_the_date_in_it()
  {
    var date = WellKnown(1861, 3, 3);
    var dialog = await CreateDialogAsync(new TestServices(), date);
    var view = await OtherCalendarViewAsync(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.OtherCalendar = DisplayCalendar.Julian;
      dialog.OtherCalendarSwitch = true;
    });

    Assert.Equal(("1861", 1, 18), (view.Year, view.MonthIndex, view.DayIndex));
    Assert.Equal(date, dialog.OtherCalendarDate);
  }

  // Hebrew starts in 1583.
  [Theory]
  [InlineData(1861, 0, 0, DateStatus.MonthUnknown, DisplayCalendar.Julian)]
  [InlineData(1500, 1, 1, DateStatus.WellKnown, DisplayCalendar.Hebrew)]
  public async Task Turning_the_other_calendar_on_converts_only_a_full_date_it_can_show(
    int year, int month, int day, DateStatus status, DisplayCalendar calendar)
  {
    var dialog = await CreateDialogAsync(new TestServices(), Date.Create(year, month, day, status));
    var view = await OtherCalendarViewAsync(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.OtherCalendar = calendar;
      dialog.OtherCalendarSwitch = true;
    });

    Assert.Equal("0", view.Year);
    Assert.Null(dialog.OtherCalendarDate);
  }

  [Fact]
  public async Task Turning_the_other_calendar_off_shows_its_date_in_the_Gregorian_fields()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);
    var view = await OtherCalendarViewAsync(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.OtherCalendarSwitch = true;
      view.Calendar = DisplayCalendar.Julian;
      view.Year = "1861";
      view.MonthIndex = 1;
      view.DayIndex = 18;
      dialog.OtherCalendarSwitch = false;
    });

    Assert.Equal(("1861", dialog.Months[2], dialog.Days[2]), (dialog.Year, dialog.Month, dialog.Day));
    Assert.True(dialog.DaySwitch);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    Assert.Equal(WellKnown(1861, 3, 3), await dialog.Info);
  }

  // Attached, the native pickers push their selections back through the bindings.
  [Fact]
  public async Task Attached_the_other_calendar_takes_the_date_without_touching_it()
  {
    var date = WellKnown(1861, 3, 3);
    var dialog = await CreateDialogAsync(new TestServices(), date);
    await using var window = await WindowHost.AttachAsync(dialog);
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog.GetVisualTreeDescendants().OfType<Picker>().All(p => p.IsLoaded)),
      loaded => loaded,
      timeoutMessage: "The pickers never loaded.");
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      dialog.OtherCalendar = DisplayCalendar.Julian;
      dialog.OtherCalendarSwitch = true;
    });
    var view = await OtherCalendarViewAsync(dialog);
    var pickers = await MainThread.InvokeOnMainThreadAsync(() => view.GetVisualTreeDescendants().OfType<Picker>().ToArray());
    var julian = Array.IndexOf(Enum.GetValues<DisplayCalendar>(), DisplayCalendar.Julian);
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => pickers.Select(p => p.SelectedIndex).ToArray()),
      selected => selected.SequenceEqual([julian, 1, 18]),
      timeoutMessage: "The pickers never showed Julian 19 February 1861.");

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    Assert.Equal(date, await dialog.Info);
  }
}
