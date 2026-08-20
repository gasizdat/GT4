namespace GT4.UI.Utils.Settings;

// Pins the app to the light or the dark palette, or lets it keep following the OS. Every
// AppThemeBinding in the app resolves against Application.RequestedTheme, which UserAppTheme
// overrides -- so this one property is the whole of applying a theme. A DI singleton (like
// BackgroundAnimation) so ThemeSetting drives it when the user changes the setting and App calls
// Apply once at startup with the persisted value.
public sealed class Theme
{
  public const AppTheme Default = AppTheme.Unspecified;

  // A hand-edited config can name a theme that is not one of the three offered, so every read of the
  // persisted string goes through here rather than trusting it.
  public static AppTheme Parse(string? theme) =>
    Enum.TryParse<AppTheme>(theme, out var parsed) ? parsed : Default;

  public void Apply(string? theme)
  {
    var application = Application.Current;
    if (application is not null)
    {
      application.UserAppTheme = Parse(theme);
    }
  }
}
