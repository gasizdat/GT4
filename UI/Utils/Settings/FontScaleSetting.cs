using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

// Persists the user-selected font-size multiplier through the interactive app configuration so the
// choice survives restarts. The factor is surfaced to the user as a percentage (100% == unscaled)
// through the generic ISettingEditor flow, so it is edited on the SettingsPage exactly like every
// other setting. Applying the factor to the live UI is FontScale's job: this type calls FontScale.Apply
// when the value changes, then persists the clamped percentage that FontScale resolved.
internal class FontScaleSetting : ISettingEditor
{
  private const string FontScaleSection = "Appearance.FontScale";

  // Rendered in the (font-scaled) Example label so the user previews glyphs at the chosen size.
  private const string FontSample = "AaBbCc 123";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly FontScale? _FontScale;

  public FontScaleSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    FontScale? fontScale)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _FontScale = fontScale;
  }

  public string Group => nameof(FontScale);

  public string DisplayName => UIStrings.FieldFontScale;

  public string Description =>
#if ANDROID
    UIStrings.FieldFontScaleHintAndroid;
#elif WINDOWS
    UIStrings.FieldFontScaleHintWindows;
#elif MACCATALYST
    UIStrings.FieldFontScaleHintMac;
#elif IOS
    UIStrings.FieldFontScaleHintIOS;
#else 
# error  Platform Not Supported;
#endif

  public string Example => FontSample;

  public string Value
  {
    get => _Configuration[FontScaleSection] ?? "100%";
    set
    {
      _FontScale?.Apply(value);
      var factor = (int)Math.Round(100 * (_FontScale?.CurrentFactor ?? FontScale.DefaultFactor));
      var stringValue = $"{factor}%";
      _InteractiveConfiguration?.SetKey(FontScaleSection, stringValue);
    }
  }

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(FontScaleSection);
    _FontScale?.Apply(Value);
  }
}
