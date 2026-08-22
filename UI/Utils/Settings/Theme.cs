namespace GT4.UI.Utils.Settings;

// UserAppTheme overrides Application.RequestedTheme, which every AppThemeBinding in the app resolves
// against, so this one property is the whole of applying a theme.
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
