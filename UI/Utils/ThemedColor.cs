namespace GT4.UI.Utils;

// For the few call sites that paint a color from code instead of XAML (drawn shapes, code-built
// views) and so have no Setter to carry an AppThemeBinding — resolves the same Light/Dark resource
// pairing an AppThemeBinding would.
public static class ThemedColor
{
  public static Color Resolve(string resourceKey, Color fallback)
  {
    var key = Application.Current?.RequestedTheme == AppTheme.Dark ? resourceKey + "Dark" : resourceKey;
    return Application.Current?.Resources is { } resources
      && resources.TryGetValue(key, out var value)
      && value is Color color
        ? color
        : fallback;
  }
}
