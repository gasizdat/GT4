using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class DateSpanFormatSetting : ISettingEditor
{
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly DateSpanFormatKind _Kind;
  private readonly string _FormatSection;
  private readonly string _DefaultFormat;
  private readonly DateSpan _ExampleSpan;

  public DateSpanFormatSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    [ServiceKey] DateSpanFormatKind kind)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _Kind = kind;
    (_FormatSection, _DefaultFormat, _ExampleSpan) = kind switch
    {
      DateSpanFormatKind.Full => (
        "DateSpanFormatter.FullDateSpanFormat",
        "YEARS MONTHS DAYS",
        new DateSpan(25, 3, 15, DateStatus.WellKnown)),
      DateSpanFormatKind.Short => (
        "DateSpanFormatter.ShortDateSpanFormat",
        "YEARS MONTHS",
        new DateSpan(5, 6, 0, DateStatus.DayUnknown)),
      _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
  }

  public string Group => nameof(DateSpanFormatter);

  // Resolved per read: the container keeps the setting as a singleton across a language switch.
  public string DisplayName => _Kind switch
  {
    DateSpanFormatKind.Full => UIStrings.FieldDateSpanDisplayFormat,
    _ => UIStrings.FieldShortDateSpanDisplayFormat
  };

  public string Description => _Kind switch
  {
    DateSpanFormatKind.Full => UIStrings.FieldDateSpanDisplayFormatHint,
    _ => UIStrings.FieldShortDateSpanDisplayFormatHint
  };

  public string Example => DateSpanFormatter.Format(Value, _ExampleSpan);

  public string Value
  {
    get => _Configuration[_FormatSection] ?? _DefaultFormat;
    set => _InteractiveConfiguration?.SetKey(_FormatSection, value);
  }

  public void ResetToDefault() => _InteractiveConfiguration?.RemoveKey(_FormatSection);
}
