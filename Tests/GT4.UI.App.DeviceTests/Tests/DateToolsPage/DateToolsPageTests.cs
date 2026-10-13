using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Components;
using GT4.UI.Pages;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Expected texts come from the same provider's formatters, as the host's own date format, calendar
/// and language settings apply.
/// </summary>
public class DateToolsPageTests
{
  private static readonly Date Emancipation = Date.Create(1861, 3, 3, DateStatus.WellKnown);

  private static async Task<DateToolsPage> CreatePageAsync(TestServices services, string calendarSetting = nameof(DisplayCalendar.Gregorian))
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var setting = new Mock<ISettingEditor>();
    setting.SetupGet(s => s.Value).Returns(calendarSetting);
    return await MainThread.InvokeOnMainThreadAsync(
      () => ActivatorUtilities.CreateInstance<DateToolsPage>(services.Provider, setting.Object));
  }

  private static Task<CalendarDateView[]> InputViewsAsync(DateToolsPage page) =>
    MainThread.InvokeOnMainThreadAsync(() => page.GetVisualTreeDescendants().OfType<CalendarDateView>().ToArray());

  private static string NotInCalendar(TestServices services, string format, DisplayCalendar calendar, string range)
  {
    var name = services.Provider.GetRequiredService<IDateFormatter>().CalendarName(calendar);
    return string.Format(format, name, range);
  }

  [Theory]
  [InlineData(nameof(DisplayCalendar.Gregorian), DisplayCalendar.Julian)]
  [InlineData(nameof(DisplayCalendar.Hebrew), DisplayCalendar.Hebrew)]
  public async Task Converts_to_the_calendar_setting_unless_it_is_Gregorian(string setting, DisplayCalendar expected)
  {
    var page = await CreatePageAsync(new TestServices(), setting);

    Assert.Equal(DisplayCalendar.Gregorian, page.ConvertFromCalendar);
    Assert.Equal(expected, page.ConvertToCalendar);
  }

  [Fact]
  public async Task Disconnecting_the_pickers_keeps_the_entered_dates()
  {
    var page = await CreatePageAsync(new TestServices(), nameof(DisplayCalendar.Hebrew));
    await MainThread.InvokeOnMainThreadAsync(() => page.ConvertInput = Emancipation);
    await using (await WindowHost.AttachAsync(page))
    {
      await Poll.UntilAsync(
        () => MainThread.InvokeOnMainThreadAsync(() => page.GetVisualTreeDescendants().OfType<Picker>().All(p => p.IsLoaded)),
        loaded => loaded,
        timeoutMessage: "The pickers never loaded.");
      await MainThread.InvokeOnMainThreadAsync(page.DisconnectHandlers);
    }

    Assert.Equal(DisplayCalendar.Gregorian, page.ConvertFromCalendar);
    Assert.Equal(DisplayCalendar.Hebrew, page.ConvertToCalendar);
    Assert.Equal(Emancipation, page.ConvertInput);
  }

  [Fact]
  public async Task The_result_follows_the_input()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await MainThread.InvokeOnMainThreadAsync(() => page.ConvertInput = Emancipation);

    var expected = services.Provider.GetRequiredService<IDateFormatter>().ToString(Emancipation, DisplayCalendar.Julian);
    Assert.Equal(expected, page.ConvertResultText);
  }

  [Fact]
  public async Task The_result_follows_the_target_calendar()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    await MainThread.InvokeOnMainThreadAsync(() => page.ConvertInput = Emancipation);
    var changed = new List<string?>();
    page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => page.ConvertToCalendar = DisplayCalendar.Hebrew);

    Assert.Contains(nameof(DateToolsPage.ConvertResultText), changed);
    var expected = services.Provider.GetRequiredService<IDateFormatter>().ToString(Emancipation, DisplayCalendar.Hebrew);
    Assert.Equal(expected, page.ConvertResultText);
  }

  [Fact]
  public async Task A_result_outside_the_target_calendar_says_why()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.ConvertInput = Emancipation;
      page.ConvertToCalendar = DisplayCalendar.FrenchRepublican;
    });

    var expected = NotInCalendar(
      services, UIStrings.DateToolsResultNotRepresentable_2, DisplayCalendar.FrenchRepublican, UIStrings.DateToolsRangeFrenchRepublican);
    Assert.Equal(expected, page.ConvertResultText);
  }

  [Fact]
  public async Task An_input_outside_its_calendar_says_why()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var from = (await InputViewsAsync(page))[0];

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      from.Calendar = DisplayCalendar.Hebrew;
      from.Year = "5342";
    });

    Assert.Null(page.ConvertInput);
    var expected = NotInCalendar(
      services, UIStrings.DateToolsInputNotRepresentable_2, DisplayCalendar.Hebrew, UIStrings.DateToolsRangeHebrew);
    Assert.Equal(expected, page.ConvertResultText);
  }

  [Fact]
  public async Task Swap_moves_the_result_into_the_input()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var from = (await InputViewsAsync(page))[0];
    await MainThread.InvokeOnMainThreadAsync(() => page.ConvertInput = Emancipation);

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("Swap"));

    Assert.Equal(DisplayCalendar.Julian, page.ConvertFromCalendar);
    Assert.Equal(DisplayCalendar.Gregorian, page.ConvertToCalendar);
    Assert.Equal(Emancipation, page.ConvertInput);
    Assert.Equal(("1861", 1, 18), (from.Year, from.MonthIndex, from.DayIndex));
    var expected = services.Provider.GetRequiredService<IDateFormatter>().ToString(Emancipation, DisplayCalendar.Gregorian);
    Assert.Equal(expected, page.ConvertResultText);
  }

  [Fact]
  public async Task Swap_without_a_result_swaps_only_the_calendars()
  {
    var page = await CreatePageAsync(new TestServices());
    var from = (await InputViewsAsync(page))[0];
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.ConvertInput = Emancipation;
      page.ConvertToCalendar = DisplayCalendar.FrenchRepublican;
    });

    await MainThread.InvokeOnMainThreadAsync(() => page.PageCommand.Execute("Swap"));

    Assert.Equal(DisplayCalendar.FrenchRepublican, page.ConvertFromCalendar);
    Assert.Equal(DisplayCalendar.Gregorian, page.ConvertToCalendar);
    Assert.Equal(("1861", 2, 2), (from.Year, from.MonthIndex, from.DayIndex));
    Assert.Null(page.ConvertInput);
  }

  // 1900 is not a Gregorian leap year: 29 days, where the Y/M/D form borrows a 30-day month.
  [Fact]
  public async Task Period_shows_the_span_and_the_exact_day_count_in_either_order()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var earlier = Date.Create(1900, 1, 31, DateStatus.WellKnown);
    var later = Date.Create(1900, 3, 1, DateStatus.WellKnown);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      page.PeriodFrom = later;
      page.PeriodTo = earlier;
    });

    var span = services.Provider.GetRequiredService<IDateSpanFormatter>().ToString(later - earlier);
    Assert.Equal(string.Format(UIStrings.DateToolsPeriodResult_2, span, 29), page.PeriodText);
  }

  [Fact]
  public async Task Period_between_the_same_date_is_empty()
  {
    var page = await CreatePageAsync(new TestServices());

    Assert.Equal(page.PeriodFrom, page.PeriodTo);
    Assert.Empty(page.PeriodText);
  }

  [Fact]
  public async Task Period_with_an_endpoint_outside_its_calendar_says_why()
  {
    var services = new TestServices();
    var page = await CreatePageAsync(services);
    var to = (await InputViewsAsync(page))[2];

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      to.Calendar = DisplayCalendar.FrenchRepublican;
      to.Year = "15";
    });

    var expected = NotInCalendar(
      services, UIStrings.DateToolsInputNotRepresentable_2, DisplayCalendar.FrenchRepublican, UIStrings.DateToolsRangeFrenchRepublican);
    Assert.Equal(expected, page.PeriodText);
  }
}
