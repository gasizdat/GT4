using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class CalendarSetting : ISettingEditor
{
  private const string CalendarSection = "DateFormatter.Calendar";

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;

  public CalendarSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
  }

  public string Group => nameof(DateFormatter);

  public string DisplayName => UIStrings.FieldCalendar;

  public string Description => UIStrings.FieldCalendarHint;

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
    get => CalendarConversion.Parse(_Configuration[CalendarSection]).ToString();
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
