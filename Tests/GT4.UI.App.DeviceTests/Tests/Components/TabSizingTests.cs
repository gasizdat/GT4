using GT4.UI.Components;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// A tab shares its strip with the markdown editor's link buttons, which are on screen only while
/// the editor tab is selected. Left to fill the row, a tab changed height as they came and went.
/// </summary>
public class TabSizingTests
{
  [Fact]
  public async Task A_taller_neighbour_does_not_stretch_a_tab()
  {
    var alone = await ChromeHeightAsync(null);
    var beside = await ChromeHeightAsync(new BoxView { HeightRequest = 80 });

    Assert.Equal(alone, beside, 1);
  }

  [Fact]
  public async Task A_tab_clears_the_touch_target()
  {
    var height = await ChromeHeightAsync(null);

    Assert.True(height >= 44, $"a tab is {height:F1} tall, under the 44 touch target.");
  }

  // Both strips that hold tabs give the row its own height, so the page hands the strip down the
  // same way rather than stretching it over the whole window.
  private static async Task<double> ChromeHeightAsync(View? neighbour)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var tab = await MainThread.InvokeOnMainThreadAsync(() => new TabItemView { Text = "Editor" });

    var page = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var strip = new HorizontalStackLayout { tab };
      if (neighbour is not null)
      {
        strip.Add(neighbour);
      }

      return new ContentPage { Content = new VerticalStackLayout { strip } };
    });

    await using var window = await WindowHost.AttachAsync(page);

    var chrome = (Border)tab.Content!;
    var frame = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => chrome.Frame),
      frame => frame.Height > 0,
      timeoutMessage: "The tab was never arranged.");

    return frame.Height;
  }
}
