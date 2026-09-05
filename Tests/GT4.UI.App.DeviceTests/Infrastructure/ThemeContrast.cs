using Microsoft.Maui.ApplicationModel;

namespace GT4.UI.DeviceTests;

internal static class ThemeContrast
{
  /// <summary>
  /// Resolves values against a real UserAppTheme on the UI thread and restores the runner's own
  /// theme afterwards. Reading a colour key instead would resolve no AppThemeBinding at all.
  /// </summary>
  public static async Task<T> UnderThemeAsync<T>(AppTheme theme, Func<T> resolve)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var application = Application.Current!;
    var restore = application.UserAppTheme;
    try
    {
      return await MainThread.InvokeOnMainThreadAsync(() =>
      {
        application.UserAppTheme = theme;
        return resolve();
      });
    }
    finally
    {
      await MainThread.InvokeOnMainThreadAsync(() => application.UserAppTheme = restore);
    }
  }

  // The palette is authored as ink at an alpha, so a ratio taken from a token describes a colour
  // that never reaches the screen.
  public static Color Over(Color layer, Color ground)
  {
    var alpha = layer.Alpha;
    var red = (layer.Red * alpha) + (ground.Red * (1 - alpha));
    var green = (layer.Green * alpha) + (ground.Green * (1 - alpha));
    var blue = (layer.Blue * alpha) + (ground.Blue * (1 - alpha));
    return new Color(red, green, blue);
  }

  public static double Ratio(Color first, Color second)
  {
    var one = RelativeLuminance(first);
    var other = RelativeLuminance(second);
    var lighter = Math.Max(one, other);
    var darker = Math.Min(one, other);
    return (lighter + 0.05) / (darker + 0.05);
  }

  public static double RelativeLuminance(Color color)
  {
    var red = ToLinear(color.Red);
    var green = ToLinear(color.Green);
    var blue = ToLinear(color.Blue);
    return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
  }

  private static double ToLinear(float channel) =>
    channel <= 0.03928f ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
