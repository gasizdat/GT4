using GT4.UI.Utils;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// ThemedColor is the seam FamilyTreePage's connectors and FamilyTreeNodeView's ring resolve their
/// colour through, since neither can carry an AppThemeBinding (drawn shapes and code-built views have
/// no Setter). Confirms it actually switches key per theme instead of always resolving the light one,
/// and that both variants stay visible against the page ground.
/// </summary>
public class ThemedColorTests
{
  private const double NonTextContrast = 3.0;

  [Theory]
  [InlineData("Primary", "PrimaryDark")]
  [InlineData("Accent", "AccentDark")]
  public async Task Resolve_SwitchesToTheDarkKeyOnlyUnderDarkTheme(string lightKey, string darkKey)
  {
    var light = await ResolveAsync(AppTheme.Light, lightKey);
    var dark = await ResolveAsync(AppTheme.Dark, lightKey);

    Assert.Equal(await ResolveKeyAsync(AppTheme.Light, lightKey), light);
    Assert.Equal(await ResolveKeyAsync(AppTheme.Dark, darkKey), dark);
    Assert.NotEqual(light, dark);
  }

  [Theory]
  [InlineData("Primary")]
  [InlineData("Accent")]
  public async Task Resolve_SeparatesFromThePageGroundInBothThemes(string resourceKey)
  {
    foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
    {
      var painted = await ResolveOverPageAsync(theme, resourceKey);
      var ratio = ThemeContrast.Ratio(painted.Color, painted.Page);

      Assert.True(
        ratio >= NonTextContrast,
        $"{resourceKey} {painted.Color.ToArgbHex()} on the {theme} page {painted.Page.ToArgbHex()} is {ratio:F2}:1.");
    }
  }

  private static Task<Color> ResolveAsync(AppTheme theme, string resourceKey) =>
    ThemeContrast.UnderThemeAsync(theme, () => ThemedColor.Resolve(resourceKey, Colors.Magenta));

  private static Task<Color> ResolveKeyAsync(AppTheme theme, string resourceKey) =>
    ThemeContrast.UnderThemeAsync(theme, () => (Color)Application.Current!.Resources[resourceKey]);

  private static Task<PaintedColor> ResolveOverPageAsync(AppTheme theme, string resourceKey)
  {
    return ThemeContrast.UnderThemeAsync(theme, () =>
    {
      var color = ThemedColor.Resolve(resourceKey, Colors.Magenta);
      var page = new ContentPage();
      return new PaintedColor(color, page.BackgroundColor);
    });
  }

  private sealed record PaintedColor(Color Color, Color Page);
}
