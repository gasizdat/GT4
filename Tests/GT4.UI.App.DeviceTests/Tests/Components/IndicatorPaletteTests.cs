using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// DateCalendarPage paints its event dots straight from these keys with no AppThemeBinding, so a
/// single value has to carry both page grounds — which is what caps how light or dark they may go.
/// </summary>
public class IndicatorPaletteTests
{
  private const double NonTextContrast = 3.0;

  [Theory]
  [InlineData("Accent")]
  [InlineData("Birth")]
  [InlineData("Death")]
  public async Task An_unthemed_indicator_separates_from_both_page_grounds(string key)
  {
    foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
    {
      var painted = await ResolveAsync(theme, key);
      var dot = ThemeContrast.Over(painted.Indicator, painted.Page);
      var ratio = ThemeContrast.Ratio(dot, painted.Page);

      Assert.True(
        ratio >= NonTextContrast,
        $"{key} {dot.ToArgbHex()} on the {theme} page {painted.Page.ToArgbHex()} is {ratio:F2}:1.");
    }
  }

  private static Task<PaintedIndicator> ResolveAsync(AppTheme theme, string key)
  {
    return ThemeContrast.UnderThemeAsync(theme, () =>
    {
      var indicator = (Color)Application.Current!.Resources[key];
      var page = new ContentPage();
      return new PaintedIndicator(indicator, page.BackgroundColor);
    });
  }

  private sealed record PaintedIndicator(Color Indicator, Color Page);
}
