using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class DateFormatSetting : ISettingEditor
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly string _FormatSection;
  private readonly string _DefaultFormat;
  private readonly Date _ExampleDate;

  public DateFormatSetting(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IInteractiveConfiguration? interactiveConfiguration,
    string formatSection,
    string defaultFormat,
    string displayName,
    string description,
    Date exampleDate)
  {
    _ServiceProvider = serviceProvider;
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _FormatSection = formatSection;
    _DefaultFormat = defaultFormat;
    DisplayName = displayName;
    Description = description;
    _ExampleDate = exampleDate;
  }

  public string Group => nameof(DateFormatter);

  public string DisplayName { get; }

  public string Description { get; }

  public string Example => _ServiceProvider
    .GetRequiredService<IDateFormatter>()
    .ToString(_ExampleDate);

  public string Value
  {
    get => _Configuration[_FormatSection] ?? _DefaultFormat;
    set => _InteractiveConfiguration?.SetKey(_FormatSection, value);
  }

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(_FormatSection);
  }
}
