using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class CalendarSetting : ISettingEditor
{
  private const string CalendarSection = "DateFormatter.Calendar";

  // Never Feb 29 (1800 isn't a Gregorian leap year) and well clear of French Republican's unmodeled
  // complementary days (which sit only in the week before the Sept epoch) -- representable everywhere.
  private static readonly Date FallbackExampleDate = Date.Create(1800, 1, 1, DateStatus.WellKnown);

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

  public string Example => _DateFormatter.ToString(ResolveExampleDate(Date.Now, Value));

  public string Value
  {
    get => CalendarConversion.ToDisplayCalendar(_Configuration[CalendarSection]).ToString();
    set => _InteractiveConfiguration?.SetKey(CalendarSection, value);
  }

  public SettingKind Kind => new SettingKind.Choice(Options);

  public void ResetToDefault() => _InteractiveConfiguration?.RemoveKey(CalendarSection);

  // Forcing today's real month/day onto year 1800 isn't always safe: Feb 29 doesn't exist in 1800,
  // and French Republican's unmodeled complementary days sit at a fixed point in the real calendar
  // year (the week before the Sept epoch) that recurs every year regardless of which year hosts it.
  // Internal (not private) so this is testable without depending on the system clock.
  internal static Date ResolveExampleDate(Date today, string calendarValue)
  {
    var date = today with { Year = 1800 };
    var isRepresentable = date is not { Month: 2, Day: 29 } &&
      (calendarValue != nameof(DisplayCalendar.FrenchRepublican) ||
        FrenchRepublicanCalendar.TryFromGregorian(new DateTime(date.Year, date.Month, date.Day), out _));

    return isRepresentable ? date : FallbackExampleDate;
  }

  // Rebuilt per read so the labels re-resolve after a language switch.
  private static SettingKind.Option[] Options =>
  [
    new(nameof(DisplayCalendar.Gregorian), DateFormatter.CalendarLabel(DisplayCalendar.Gregorian)),
    new(nameof(DisplayCalendar.Julian), DateFormatter.CalendarLabel(DisplayCalendar.Julian)),
    new(nameof(DisplayCalendar.Hebrew), DateFormatter.CalendarLabel(DisplayCalendar.Hebrew)),
    new(nameof(DisplayCalendar.FrenchRepublican), DateFormatter.CalendarLabel(DisplayCalendar.FrenchRepublican)),
  ];
}
