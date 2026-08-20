using GT4.UI.Utils.Settings;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Theme's write to the running Application, which GT4.UI.View.Tests cannot reach: there
/// Application.Current is null and Apply only no-ops. UserAppTheme is what every AppThemeBinding in
/// the app resolves against, so landing it on the live Application is what makes a picked theme
/// reach the UI at all.
/// </summary>
public class ThemeTests
{
  [Theory]
  [InlineData("Light", AppTheme.Light)]
  [InlineData("Dark", AppTheme.Dark)]
  [InlineData("Unspecified", AppTheme.Unspecified)]
  [InlineData("Sepia", AppTheme.Unspecified)]
  public async Task Apply_pins_the_running_application_to_the_named_theme(string persisted, AppTheme expected)
  {
    var application = Application.Current!;
    var restore = application.UserAppTheme;
    try
    {
      await MainThread.InvokeOnMainThreadAsync(() => new Theme().Apply(persisted));

      Assert.Equal(expected, application.UserAppTheme);
    }
    finally
    {
      await MainThread.InvokeOnMainThreadAsync(() => application.UserAppTheme = restore);
    }
  }
}
