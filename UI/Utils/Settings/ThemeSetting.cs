using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class ThemeSetting : ISettingEditor
{
  private const string ThemeSection = "Appearance.Theme";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly Theme? _Theme;

  public ThemeSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    Theme? theme)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _Theme = theme;
  }

  public string Group => nameof(Theme);

  public string DisplayName => UIStrings.FieldTheme;

  public string Description => UIStrings.FieldThemeHint;

  public string Example
  {
    get
    {
      var selected = Options.Single(o => o.Value == Value);
      return selected.Label;
    }
  }

  public string Value
  {
    get
    {
      var configured = _Configuration[ThemeSection];
      var theme = Theme.Parse(configured);
      return theme.ToString();
    }
    set
    {
      _Theme?.Apply(value);
      _InteractiveConfiguration?.SetKey(ThemeSection, value);
    }
  }

  public SettingKind Kind => new SettingKind.Choice(Options);

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(ThemeSection);
    _Theme?.Apply(Value);
  }

  // Rebuilt per read so the labels re-resolve after a language switch.
  private static SettingKind.Option[] Options =>
  [
    new(nameof(AppTheme.Unspecified), UIStrings.FieldThemeSystem),
    new(nameof(AppTheme.Light), UIStrings.FieldThemeLight),
    new(nameof(AppTheme.Dark), UIStrings.FieldThemeDark),
  ];
}
