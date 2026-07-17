using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SelectDateDialog directly: it has no IServiceProvider/navigation dependency at all, just
/// a real (unmocked) IDateFormatter, so it's constructed straight from TestServices' real container.
/// The switch cascade rules (YearSwitch/MonthSwitch/DaySwitch/ApproximateSwitch) are the page's own
/// non-obvious logic and the main thing worth pinning here.
/// </summary>
public class SelectDateDialogTests
{
  private static async Task<SelectDateDialog> CreateDialogAsync(TestServices services, Date? date)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var dateFormatter = services.Provider.GetRequiredService<IDateFormatter>();
    return await MainThread.InvokeOnMainThreadAsync(() => new SelectDateDialog(date, dateFormatter));
  }

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

    await MainThread.InvokeOnMainThreadAsync(() => dialog.OnSelectDateBtn(dialog, EventArgs.Empty));
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
  public async Task OnSelectDateBtn_returns_null_when_not_ready()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.OnSelectDateBtn(dialog, EventArgs.Empty));
    var result = await dialog.Info;

    Assert.Null(result);
  }

  [Fact]
  public async Task OnSelectDateBtn_returns_the_selected_date_once_touched()
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

    await MainThread.InvokeOnMainThreadAsync(() => dialog.OnSelectDateBtn(dialog, EventArgs.Empty));
    var result = await dialog.Info;

    Assert.Equal(Date.Create(1990, 3, 10, DateStatus.WellKnown), result);
  }
}
