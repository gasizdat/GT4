using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// The implicit Button style binds its background per theme, and the dark half went unnoticed for the
/// life of the app: it still pointed at the MAUI template's purple while the light half used the app's
/// green. Resolving a real Button under a real UserAppTheme is the only check that sees what a user
/// sees; reading the colour key alone would have passed throughout.
/// </summary>
public class ButtonPaletteTests
{
  private static async Task<Color> ResolveButtonBackgroundAsync(AppTheme theme)
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
        return button.BackgroundColor;
      });
    }
    finally
    {
      await MainThread.InvokeOnMainThreadAsync(() => application.UserAppTheme = restore);
    }
  }

  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task A_button_is_green_in_either_theme(AppTheme theme)
  {
    var background = await ResolveButtonBackgroundAsync(theme);

    Assert.True(
      background.Green > background.Red && background.Green > background.Blue,
      $"{theme} button background {background.ToArgbHex()} is not in the app's green family.");
  }

  [Fact]
  public async Task The_dark_button_is_a_lighter_green_than_the_light_one()
  {
    var light = await ResolveButtonBackgroundAsync(AppTheme.Light);
    var dark = await ResolveButtonBackgroundAsync(AppTheme.Dark);

    Assert.True(
      dark.GetLuminosity() > light.GetLuminosity(),
      $"dark-theme button {dark.ToArgbHex()} must read as the lighter sibling of {light.ToArgbHex()}.");
  }
}
