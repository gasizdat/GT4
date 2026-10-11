using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class FanChartSweepSetting : ISettingEditor
{
  private const string SweepSection = "FanChart.Sweep";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;

  public FanChartSweepSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
  }

  public string Group => nameof(FanChartSweep);

  public string DisplayName => UIStrings.FieldFanChartSweep;

  public string Description => UIStrings.FieldFanChartSweepHint;

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
      var configured = _Configuration[SweepSection];
      var sweep = FanChartSweep.Parse(configured);
      return sweep.ToString();
    }
    set => _InteractiveConfiguration?.SetKey(SweepSection, value);
  }

  public SettingKind Kind => new SettingKind.Choice(Options);

  public void ResetToDefault() => _InteractiveConfiguration?.RemoveKey(SweepSection);

  private static SettingKind.Option[] Options =>
    [.. FanChartSweep.Options.Select(sweep => new SettingKind.Option($"{sweep}", $"{sweep}°"))];
}
