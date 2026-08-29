using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class DateFormatSetting : ISettingEditor
{
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly string _FormatSection;
  private readonly string _DefaultFormat;
  private readonly Func<string> _DisplayName;
  private readonly Func<string> _Description;
  private readonly Date _ExampleDate;

  public DateFormatSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    [ServiceKey] DateFormatKind kind)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    // The localized slots stay deferred: the container keeps the setting as a singleton across a
    // language switch.
    (string Section,
      string DefaultFormat,
      Func<string> DisplayName,
      Func<string> Description,
      Date ExampleDate) parts = kind switch
      {
        DateFormatKind.Full => (
          "DateFormatter.FullDateFormat",
          "DD MM YYYY",
          () => UIStrings.FieldDateDisplayFormat,
          () => UIStrings.FieldDateDisplayFormatHint,
          Date.Now),
        DateFormatKind.Short => (
          "DateFormatter.ShortDateFormat",
          "MM YYYY",
          () => UIStrings.FieldShortDateDisplayFormat,
          () => UIStrings.FieldShortDateDisplayFormatHint,
          Date.Now with { Status = DateStatus.DayUnknown }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
      };
    (_FormatSection, _DefaultFormat, _DisplayName, _Description, _ExampleDate) = parts;
  }

  public string Group => nameof(DateFormatter);

  public string DisplayName => _DisplayName();

  public string Description => _Description();

  public string Example => DateFormatter.Format(Value, _ExampleDate);

  public string Value
  {
    get => _Configuration[_FormatSection] ?? _DefaultFormat;
    set => _InteractiveConfiguration?.SetKey(_FormatSection, value);
  }

  public void ResetToDefault() => _InteractiveConfiguration?.RemoveKey(_FormatSection);
}
