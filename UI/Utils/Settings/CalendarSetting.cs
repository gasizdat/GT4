using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class CalendarSetting : ISettingEditor
{
  private const string CalendarSection = "DateFormatter.Calendar";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly DateFormatter _DateFormatter;

  public CalendarSetting(
    IConfiguration configuration,
    [FromKeyedServices(DateFormatKind.Full)]
    ISettingEditor fullDateFormatSetting,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _DateFormatter = new DateFormatter(fullDateFormatSetting, fullDateFormatSetting, this);
  }

  public string Group => nameof(DateFormatter);

  public string DisplayName => UIStrings.FieldCalendar;

  public string Description => UIStrings.FieldCalendarHint;

  public string Example => _DateFormatter.ToString(Date.Now with { Year = 1800 });

  public string Value
  {
    get => CalendarConversion.ToDisplayCalendar(_Configuration[CalendarSection]).ToString();
    set => _InteractiveConfiguration?.SetKey(CalendarSection, value);
  }

  public SettingKind Kind => new SettingKind.Choice(Options);

  public void ResetToDefault() => _InteractiveConfiguration?.RemoveKey(CalendarSection);

  // Rebuilt per read so the labels re-resolve after a language switch.
  private static SettingKind.Option[] Options =>
  [
    new(nameof(DisplayCalendar.Gregorian), DateFormatter.CalendarLabel(DisplayCalendar.Gregorian)),
    new(nameof(DisplayCalendar.Julian), DateFormatter.CalendarLabel(DisplayCalendar.Julian)),
    new(nameof(DisplayCalendar.Hebrew), DateFormatter.CalendarLabel(DisplayCalendar.Hebrew)),
    new(nameof(DisplayCalendar.FrenchRepublican), DateFormatter.CalendarLabel(DisplayCalendar.FrenchRepublican)),
  ];
}
