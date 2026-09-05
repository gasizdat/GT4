using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Resolving a real Button under a real UserAppTheme is the only check that sees what a user sees: a
/// theme-bound colour can point anywhere — the dark half once pointed at the MAUI template's purple
/// for the life of the app — and reading the colour keys would pass regardless. The dark button is
/// outlined, its body recessed almost into the page, so the border carries the whole affordance.
/// </summary>
public class ButtonPaletteTests
{
  private const double TextContrast = 4.5;
  private const double NonTextContrast = 3.0;

  [Theory]
  [InlineData(AppTheme.Light, "Normal")]
  [InlineData(AppTheme.Light, "PointerOver")]
  [InlineData(AppTheme.Dark, "Normal")]
  [InlineData(AppTheme.Dark, "PointerOver")]
  public async Task A_button_caption_is_legible_on_its_own_body(AppTheme theme, string state)
  {
    var chrome = await ResolveButtonChromeAsync(theme, state);
    var body = ThemeContrast.Over(chrome.Background, chrome.Page);
    var caption = ThemeContrast.Over(chrome.Text, body);
    var ratio = ThemeContrast.Ratio(caption, body);

    Assert.True(
      ratio >= TextContrast,
      $"{theme} {state} caption {caption.ToArgbHex()} on body {body.ToArgbHex()} is {ratio:F2}:1.");
  }

  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task A_button_border_separates_from_the_page(AppTheme theme)
  {
    var chrome = await ResolveButtonChromeAsync(theme, "Normal");
    var border = ThemeContrast.Over(chrome.Border, chrome.Page);
    var ratio = ThemeContrast.Ratio(border, chrome.Page);

    Assert.True(
      ratio >= NonTextContrast,
      $"{theme} button border {border.ToArgbHex()} on page {chrome.Page.ToArgbHex()} is {ratio:F2}:1.");
  }

  private static Task<ButtonChrome> ResolveButtonChromeAsync(AppTheme theme, string state)
  {
    return ThemeContrast.UnderThemeAsync(theme, () =>
    {
      var button = new Button();
      var moved = VisualStateManager.GoToState(button, state);
      Assert.True(moved, $"the Button style has no visual state named {state}.");

      var page = new ContentPage();
      return new ButtonChrome(button.BackgroundColor, button.BorderColor, button.TextColor, page.BackgroundColor);
    });
  }

  private sealed record ButtonChrome(Color Background, Color Border, Color Text, Color Page);
}
