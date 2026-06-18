using GT4.Core.Utils;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace GT4.UI.Utils.Settings;

// Persists the user-selected font-size multiplier through the interactive app configuration so the
// choice survives restarts. The factor scales every font size in the app (see FontScale). Like
// LanguageSetting this is not an ISettingEditor: it is driven by a dedicated Stepper on the
// SettingsPage rather than edited as free text.
public class FontScaleSetting
{
  private const string FontScaleSection = "Appearance.FontScale";

  public const double DefaultFactor = 1.0;
  public const double MinFactor = 0.75;
  public const double MaxFactor = 2.0;
  public const double StepFactor = 0.05;

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;

  public FontScaleSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
  }

  public double Value
  {
    // Fall back to the unscaled baseline when nothing has been persisted yet, and clamp on read so a
    // hand-edited or out-of-range config value can never push the UI past the supported bounds.
    get => double.TryParse(
        _Configuration[FontScaleSection],
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out var value)
      ? Clamp(value)
      : DefaultFactor;
    set => _InteractiveConfiguration?.SetKey(
      FontScaleSection,
      Clamp(value).ToString(CultureInfo.InvariantCulture));
  }

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(FontScaleSection);
  }

  public static double Clamp(double value) => Math.Clamp(value, MinFactor, MaxFactor);
}
