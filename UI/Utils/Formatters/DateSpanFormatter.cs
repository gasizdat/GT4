using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Settings;

namespace GT4.UI.Utils.Formatters;

internal class DateSpanFormatter : IDateSpanFormatter
{
  private readonly ISettingEditor _FullDateSpanFormatSetting;
  private readonly ISettingEditor _ShortDateSpanFormatSetting;

  public DateSpanFormatter(
    [FromKeyedServices(DateSpanFormatKind.Full)] ISettingEditor fullDateSpanFormatSetting,
    [FromKeyedServices(DateSpanFormatKind.Short)] ISettingEditor shortDateSpanFormatSetting)
  {
    _FullDateSpanFormatSetting = fullDateSpanFormatSetting;
    _ShortDateSpanFormatSetting = shortDateSpanFormatSetting;
  }

  public string ToString(DateSpan? dateSpan)
  {
    var ret = string.Empty;

    if (dateSpan.HasValue)
    {
      ret = dateSpan.Value.Status switch
      {
        DateStatus.WellKnown => Format(_FullDateSpanFormatSetting.Value, dateSpan.Value),
        DateStatus.DayUnknown => Format(_ShortDateSpanFormatSetting.Value, dateSpan.Value),
        DateStatus.MonthUnknown => YearsFormat(dateSpan.Value.Years),
        DateStatus.YearApproximate when dateSpan.Value.Years == 0 => string.Empty,
        DateStatus.YearApproximate => string.Format(UIStrings.DateStatusYearApproximate_1, YearsFormat(dateSpan.Value.Years)),
        _ => string.Empty
      };
    }

    return string.IsNullOrWhiteSpace(ret) ? UIStrings.DateStatusUnknown : ret;
  }

  /// <summary>Applies an arbitrary format string to a date span, independent of any configured
  /// setting. Stateless, so callers that already hold the format they want (e.g. a setting previewing
  /// its own configured value) don't need an <see cref="IDateSpanFormatter"/> instance to use it.</summary>
  public static string Format(string format, DateSpan dateSpan)
  {
    var years = () => YearsFormat(dateSpan.Years);
    var months = () => MonthsFormat(dateSpan.Months);
    var days = () => DaysFormat(dateSpan.Days);
    return TemplateInterpolator.Format(format, new Dictionary<string, Func<string>>()
    {
      { "YEARS", years },
      { "MONTHS", months },
      { "DAYS", days },
    });
  }

  protected static string DaysFormat(int days) =>
    Language.Current switch
    {
      _ when days == 0 => string.Empty,
      var language when language == Language.RU => RussianNumeralsDeclension(days, "{0} день", "{0} дня", "{0} дней"),
      var language when language == Language.DE => SingularPluralDeclension(days, "{0} Tag", "{0} Tage"),
      _ => SingularPluralDeclension(days, "{0} day", "{0} days")
    };

  protected static string MonthsFormat(int months) =>
    Language.Current switch
    {
      _ when months == 0 => string.Empty,
      var language when language == Language.RU => RussianNumeralsDeclension(months, "{0} месяц", "{0} месяца", "{0} месяцев"),
      var language when language == Language.DE => SingularPluralDeclension(months, "{0} Monat", "{0} Monate"),
      _ => SingularPluralDeclension(months, "{0} month", "{0} months")
    };

  protected static string YearsFormat(int years) =>
    Language.Current switch
    {
      _ when years == 0 => string.Empty,
      var language when language == Language.RU => RussianNumeralsDeclension(years, "{0} год", "{0} года", "{0} лет"),
      var language when language == Language.DE => SingularPluralDeclension(years, "{0} Jahr", "{0} Jahre"),
      _ => SingularPluralDeclension(years, "{0} year", "{0} years")
    };

  protected static string SingularPluralDeclension(int value, string single, string many) => string.Format(value == 1 ? single : many, value);

  protected static string RussianNumeralsDeclension(int value, string single, string several, string many)
  {
    var lastTwoDigits = value % 100;
    if (lastTwoDigits >= 11 && lastTwoDigits <= 20)
    {
      return string.Format(many, value);
    }

    var lastDigit = value % 10;
    switch (lastDigit)
    {
      case 1:
        return string.Format(single, value);
      case 2:
      case 3:
      case 4:
        return string.Format(several, value);
      case 0:
      default:
        return string.Format(many, value);
    }
  }
}
