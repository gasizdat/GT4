using GT4.Core.Utils;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

// Persists the user-selected UI language through the interactive app configuration so the choice
// survives restarts. Unlike the other settings this is not an ISettingEditor: the language is picked
// with a dedicated Picker on the MainPage rather than edited as free text on the SettingsPage.
public class LanguageSetting
{
  private const string LanguageSection = "Localization.Language";
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;

  public LanguageSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
  }

  public Language Value
  {
    // When no language has been persisted yet, fall back to the OS-derived language so the very
    // first launch keeps following the device culture.
    get => Language.Languages.SingleOrDefault(l => l.Code == _Configuration[LanguageSection], Language.Current);
    set => _InteractiveConfiguration?.SetKey(LanguageSection, value.Code);
  }

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(LanguageSection);
  }
}
