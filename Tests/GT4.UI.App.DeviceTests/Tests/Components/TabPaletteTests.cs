using GT4.UI.Components;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// A tab's three states are a lightness ladder in both themes, which is what lets its caption hold
/// one colour per theme rather than needing a state of its own. The state names are strings the
/// code-behind hands to GoToState, so a typo would silently leave a tab wearing its neighbour.
/// </summary>
public class TabPaletteTests
{
  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task A_tab_climbs_from_inactive_through_hovered_to_active(AppTheme theme)
  {
    var ladder = await ResolveLadderAsync(theme);

    Assert.True(
      ladder.Hovered > ladder.Inactive,
      $"{theme} hovered tab ({ladder.Hovered:F3}) is not lighter than inactive ({ladder.Inactive:F3}).");
    Assert.True(
      ladder.Active > ladder.Hovered,
      $"{theme} active tab ({ladder.Active:F3}) is not lighter than hovered ({ladder.Hovered:F3}).");
  }

  private static Task<TabLadder> ResolveLadderAsync(AppTheme theme)
  {
    return ThemeContrast.UnderThemeAsync(theme, () =>
    {
      var tab = new TabItemView();
      var chrome = (Border)tab.Content!;
      var page = new ContentPage();
      var ground = page.BackgroundColor;

      var inactive = Lightness(chrome, "Inactive", ground);
      var hovered = Lightness(chrome, "Hovered", ground);
      var active = Lightness(chrome, "Active", ground);
      return new TabLadder(inactive, hovered, active);
    });
  }

  private static double Lightness(Border chrome, string state, Color ground)
  {
    var moved = VisualStateManager.GoToState(chrome, state);
    Assert.True(moved, $"the tab has no visual state named {state}.");

    var fill = ThemeContrast.Over(chrome.BackgroundColor, ground);
    return ThemeContrast.RelativeLuminance(fill);
  }

  private sealed record TabLadder(double Inactive, double Hovered, double Active);
}
