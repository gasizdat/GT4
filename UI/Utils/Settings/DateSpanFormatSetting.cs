using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class DateSpanFormatSetting : ISettingEditor
{
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly string _FormatSection;
  private readonly string _DefaultFormat;
  private readonly Func<string> _DisplayName;
  private readonly Func<string> _Description;
  private readonly DateSpan _ExampleSpan;

  public DateSpanFormatSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    [ServiceKey] DateSpanFormatKind kind)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    // The localized slots stay deferred: the container keeps the setting as a singleton across a
    // language switch.
    (_FormatSection, _DefaultFormat, _DisplayName, _Description, _ExampleSpan) = kind switch
    {
      DateSpanFormatKind.Full => (
        "DateSpanFormatter.FullDateSpanFormat",
        "YEARS MONTHS DAYS",
        (Func<string>)(() => UIStrings.FieldDateSpanDisplayFormat),
        (Func<string>)(() => UIStrings.FieldDateSpanDisplayFormatHint),
        new DateSpan(25, 3, 15, DateStatus.WellKnown)),
      DateSpanFormatKind.Short => (
        "DateSpanFormatter.ShortDateSpanFormat",
        "YEARS MONTHS",
        (Func<string>)(() => UIStrings.FieldShortDateSpanDisplayFormat),
        (Func<string>)(() => UIStrings.FieldShortDateSpanDisplayFormatHint),
        new DateSpan(5, 6, 0, DateStatus.DayUnknown)),
      _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
  }

  public string Group => nameof(DateSpanFormatter);

  public string DisplayName => _DisplayName();

  public string Description => _Description();

  public string Example => DateSpanFormatter.Format(Value, _ExampleSpan);

  public string Value
  {
    get => _Configuration[_FormatSection] ?? _DefaultFormat;
    set => _InteractiveConfiguration?.SetKey(_FormatSection, value);
  }

  public void ResetToDefault() => _InteractiveConfiguration?.RemoveKey(_FormatSection);
}
