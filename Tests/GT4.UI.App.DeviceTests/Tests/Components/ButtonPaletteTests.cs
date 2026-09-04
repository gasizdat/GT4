using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// The implicit Button style binds its colours per theme, and the dark half went unnoticed for the
/// life of the app: it still pointed at the MAUI template's purple while the light half used the
/// app's green. Resolving a real Button under a real UserAppTheme is the only check that sees what a
/// user sees; reading the colour keys alone would have passed throughout.
///
/// These once asserted the dark button was a lighter green than the light one, which described the
/// filled design it had then. It is an outlined control now — a body recessed almost into the page,
/// carrying a light hairline — so hue and lightness are no longer the promise. What has to hold is
/// that the caption stays legible on the body, and that the border, which is the entire affordance
/// once the fill recedes, still separates from the page behind it.
/// </summary>
public class ButtonPaletteTests
{
  private const double TextContrast = 4.5;
  private const double NonTextContrast = 3.0;

  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task A_button_caption_is_legible_on_its_own_body(AppTheme theme)
  {
    var chrome = await ResolveButtonChromeAsync(theme);
    var body = Composite(chrome.Background, chrome.Page);
    var caption = Composite(chrome.Text, body);
    var ratio = Contrast(caption, body);

    Assert.True(
      ratio >= TextContrast,
      $"{theme} button caption {caption.ToArgbHex()} on body {body.ToArgbHex()} is {ratio:F2}:1.");
  }

  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task A_button_border_separates_from_the_page(AppTheme theme)
  {
    var chrome = await ResolveButtonChromeAsync(theme);
    var border = Composite(chrome.Border, chrome.Page);
    var ratio = Contrast(border, chrome.Page);

    Assert.True(
      ratio >= NonTextContrast,
      $"{theme} button border {border.ToArgbHex()} on page {chrome.Page.ToArgbHex()} is {ratio:F2}:1.");
  }

  private static async Task<ButtonChrome> ResolveButtonChromeAsync(AppTheme theme)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var application = Application.Current!;
    var restore = application.UserAppTheme;
    try
    {
      return await MainThread.InvokeOnMainThreadAsync(() =>
      {
        application.UserAppTheme = theme;
        var button = new Button();
        var page = new ContentPage();
        return new ButtonChrome(button.BackgroundColor, button.BorderColor, button.TextColor, page.BackgroundColor);
      });
    }
    finally
    {
      await MainThread.InvokeOnMainThreadAsync(() => application.UserAppTheme = restore);
    }
  }

  // The button's surfaces are translucent over whatever it sits on, so a ratio taken straight from
  // the token would describe a colour that never reaches the screen.
  private static Color Composite(Color over, Color ground)
  {
    var alpha = over.Alpha;
    var red = (over.Red * alpha) + (ground.Red * (1 - alpha));
    var green = (over.Green * alpha) + (ground.Green * (1 - alpha));
    var blue = (over.Blue * alpha) + (ground.Blue * (1 - alpha));
    return new Color(red, green, blue);
  }

  private static double Contrast(Color first, Color second)
  {
    var one = RelativeLuminance(first);
    var other = RelativeLuminance(second);
    var lighter = Math.Max(one, other);
    var darker = Math.Min(one, other);
    return (lighter + 0.05) / (darker + 0.05);
  }

  private static double RelativeLuminance(Color color)
  {
    var red = ToLinear(color.Red);
    var green = ToLinear(color.Green);
    var blue = ToLinear(color.Blue);
    return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
  }

  private static double ToLinear(float channel) =>
    channel <= 0.03928f ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

  private sealed record ButtonChrome(Color Background, Color Border, Color Text, Color Page);
}
