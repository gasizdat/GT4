using GT4.UI.Components;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// A tab's selection is a pair of DataTriggers on IsActive — one on the Border, one on the caption a
/// Border setter cannot reach — while hover is the platform's own PointerOver state writing the same
/// two Border properties. The two mechanisms collide on BackgroundColor, and the collision shows
/// only once the pointer leaves a tab that is already selected.
/// </summary>
public class TabPaletteTests
{
  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task Selecting_a_tab_repaints_its_chrome_and_its_caption(AppTheme theme)
  {
    var inactive = await ResolveChromeAsync(theme, false, "Normal");
    var active = await ResolveChromeAsync(theme, true, "Normal");

    Assert.NotEqual(inactive.Fill, active.Fill);
    Assert.NotEqual(inactive.Stroke, active.Stroke);
    Assert.NotEqual(inactive.Ink, active.Ink);
  }

  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task Hovering_an_unselected_tab_lights_its_chrome(AppTheme theme)
  {
    var resting = await ResolveChromeAsync(theme, false, "Normal");
    var hovered = await ResolveChromeAsync(theme, false, "PointerOver");

    Assert.NotEqual(resting.Fill, hovered.Fill);
    Assert.NotEqual(resting.Stroke, hovered.Stroke);
  }

  [Theory]
  [InlineData(AppTheme.Light, true)]
  [InlineData(AppTheme.Light, false)]
  [InlineData(AppTheme.Dark, true)]
  [InlineData(AppTheme.Dark, false)]
  public async Task A_tab_returns_to_its_own_fill_when_the_pointer_leaves(AppTheme theme, bool isActive)
  {
    var resting = await ResolveChromeAsync(theme, isActive, "Normal");
    var afterwards = await ResolveChromeAsync(theme, isActive, "PointerOver", "Normal");

    Assert.Equal(resting.Fill, afterwards.Fill);
    Assert.Equal(resting.Stroke, afterwards.Stroke);
  }

  private static Task<TabChrome> ResolveChromeAsync(AppTheme theme, bool isActive, params string[] states)
  {
    return ThemeContrast.UnderThemeAsync(theme, () =>
    {
      var tab = new TabItemView { Text = "Editor", IsActive = isActive };
      var chrome = (Border)tab.Content!;

      foreach (var state in states)
      {
        var moved = VisualStateManager.GoToState(chrome, state);
        Assert.True(moved, $"the tab has no visual state named {state}.");
      }

      var stroke = (SolidColorBrush)chrome.Stroke;
      var caption = (Label)chrome.Content!;
      return new TabChrome(chrome.BackgroundColor, stroke.Color, caption.TextColor);
    });
  }

  private sealed record TabChrome(Color Fill, Color Stroke, Color Ink);
}
