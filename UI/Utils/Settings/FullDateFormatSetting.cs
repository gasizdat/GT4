using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal class FullDateFormatSetting : ISettingEditor
{
  private const string FullDateFormatSection = "DateFormatter.FullDateFormat";
  private const string DefaultFullDateFormat = "DD MM YYYY";
  private readonly IServiceProvider _ServiceProvider;
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;

  public FullDateFormatSetting(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
  {
    _ServiceProvider = serviceProvider;
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
  }

  public string Group => nameof(DateFormatter);

  public string DisplayName => UIStrings.FieldDateDisplayFormat;

  public string Description => UIStrings.FieldDateDisplayFormatHint;

  public string Example => _ServiceProvider
    .GetRequiredService<IDateFormatter>()
    .ToString(Date.Now);

  public string Value
  {
    get => _Configuration[FullDateFormatSection] ?? DefaultFullDateFormat;
    set => _InteractiveConfiguration?.SetKey(FullDateFormatSection, value);
  }
}
