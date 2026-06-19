using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace GT4.UI.Utils.Settings;

// Persists the user-selected font-size multiplier through the interactive app configuration so the
// choice survives restarts. The factor is surfaced to the user as a percentage (100% == unscaled)
// through the generic ISettingEditor flow, so it is edited on the SettingsPage exactly like every
// other setting. Applying the factor to the live UI is FontScale's job: this type only raises Changed
// so the applier can refresh, which keeps the dependency one-directional (FontScale -> here).
internal class FontScaleSetting : ISettingEditor
{
  private const string FontScaleSection = "Appearance.FontScale";

  // Rendered in the (font-scaled) Example label so the user previews glyphs at the chosen size.
  private const string FontSample = "AaBbCc 123";

  public const double DefaultFactor = 1.0;
  public const double MinFactor = 0.75;
  public const double MaxFactor = 2.0;

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

  public string Description => UIStrings.FieldFontScaleHint;

  public string Example => FontSample;

  // The persisted multiplier, clamped on read so a hand-edited or out-of-range config value can never
  // push the UI past the supported bounds, and falling back to the unscaled baseline when unset.
  public double Factor =>
    double.TryParse(_Configuration[FontScaleSection], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
      ? Clamp(value)
      : DefaultFactor;

  public string Value
  {
    // Surface the persisted factor as a clamped whole-number percentage.
    get => $"{Factor * 100:0}%";
    // Accept "150" or "150%"; ignore anything unparseable so a bad keystroke never persists garbage
    // or pushes the UI past the supported bounds.
    set
    {
      if (!TryParsePercent(value, out var factor))
      {
        return;
      }

      _InteractiveConfiguration?.SetKey(FontScaleSection, Clamp(factor).ToString(CultureInfo.InvariantCulture));
      _FontScale?.Apply(Factor);
    }
  }

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(FontScaleSection);
    _FontScale?.Apply(Factor);
  }

  private static bool TryParsePercent(string? text, out double factor)
  {
    factor = DefaultFactor;
    if (string.IsNullOrWhiteSpace(text))
      return false;

    var trimmed = text.Trim().TrimEnd('%').Trim();
    if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
      return false;

    factor = Clamp(percent / 100.0);
    return true;
  }

  private static double Clamp(double value) => Math.Clamp(value, MinFactor, MaxFactor);
}
