using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal class ShortDateFormatSetting : ISettingEditor
{
  private const string ShortDateFormatSection = "DateFormatter.ShortDateFormat";
  private const string DefaultShortDateFormat = "MM YYYY";
  private readonly IServiceProvider _ServiceProvider;
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;

  public ShortDateFormatSetting(
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

  public string DisplayName => UIStrings.FieldShortDateDisplayFormat;

  public string Description => UIStrings.FieldShortDateDisplayFormatHint;

  public string Example => _ServiceProvider
    .GetRequiredService<IDateFormatter>()
    .ToString(Date.Now with { Status = DateStatus.DayUnknown });

  public string Value
  {
    get => _Configuration[ShortDateFormatSection] ?? DefaultShortDateFormat;
    set => _InteractiveConfiguration?.SetKey(ShortDateFormatSection, value);
  }
}
