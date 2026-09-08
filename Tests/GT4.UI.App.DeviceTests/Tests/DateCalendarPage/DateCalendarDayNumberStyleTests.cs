using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Pins issue #377: DateCalendarDayNumber fixed the badge's WidthRequest/HeightRequest, so at a high
/// font-scale factor the digit -- sized from LabelTextSizeDefault via the app-wide implicit Label
/// style, since this style sets no FontSize of its own -- outgrew a box that could not follow it.
/// </summary>
public class DateCalendarDayNumberStyleTests
{
  private static async Task<Style> LoadStyleAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var services = new TestServices();
    var page = await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableDateCalendarPage>());
    return (Style)page.Resources["DateCalendarDayNumber"];
  }

  [Fact]
  public async Task The_box_is_a_minimum_not_a_fixed_size()
  {
    var style = await LoadStyleAsync();
    var label = await MainThread.InvokeOnMainThreadAsync(() => new Label { Style = style });

    Assert.Equal(36, label.MinimumWidthRequest);
    Assert.Equal(36, label.MinimumHeightRequest);
    Assert.Equal(-1, label.WidthRequest);
    Assert.Equal(-1, label.HeightRequest);
  }

  // MinimumHeightRequest has no other precedent in this file (unlike MinimumWidthRequest, via
  // DateCalendarMonthLabelMinWidth) -- guards that it still floors at 1.0x. Not a fix pin: a fixed
  // 36 would pass here too.
  [Fact]
  public async Task The_badge_still_measures_at_least_36x36_at_default_scale()
  {
    var style = await LoadStyleAsync();
    var label = await MainThread.InvokeOnMainThreadAsync(() => new Label
    {
      Style = style,
      Text = "9",
      HorizontalOptions = LayoutOptions.Start,
      VerticalOptions = LayoutOptions.Start,
    });
    var host = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = label });
    await using var window = await WindowHost.AttachAsync(host);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => label.Frame),
      frame => frame.Width > 0,
      timeoutMessage: "The badge never laid out.");

    Assert.True(label.Width >= 36 && label.Height >= 36, $"Badge measured {label.Width}x{label.Height}.");
  }

  // No FontSize setter here -- the implicit Label style already drives it from LabelTextSizeDefault
  // (confirmed, not assumed), so an explicit setter would be redundant. WindowHost gives the label
  // the Parent chain a live DynamicResource update needs; a detached element has none.
  [Fact]
  public async Task The_digit_already_tracks_LabelTextSizeDefault_via_the_implicit_Label_style()
  {
    var style = await LoadStyleAsync();
    var label = await MainThread.InvokeOnMainThreadAsync(() => new Label { Style = style });
    var host = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = label });
    await using var window = await WindowHost.AttachAsync(host);

    var resources = Application.Current!.Resources;
    var original = resources["LabelTextSizeDefault"];
    try
    {
      await MainThread.InvokeOnMainThreadAsync(() => resources["LabelTextSizeDefault"] = 30.0);
      Assert.Equal(30.0, label.FontSize);
    }
    finally
    {
      await MainThread.InvokeOnMainThreadAsync(() => resources["LabelTextSizeDefault"] = original);
    }
  }
}
