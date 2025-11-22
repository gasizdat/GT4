using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.Formatters;

public class DateSpanFormatter : IDateSpanFormatter
{
  public string ToString(DateSpan? dateSpan)
  {
    // TODO use configuration

    string ret = string.Empty;

    if (dateSpan.HasValue)
    {
      ret = dateSpan.Value.Status switch
      {
        DateStatus.WellKnown => $"{YearsFormat(dateSpan.Value.Years)} {MonthsFormat(dateSpan.Value.Months)} {DaysFormat(dateSpan.Value.Days)}",
        DateStatus.DayUnknown => $"{YearsFormat(dateSpan.Value.Years)} {MonthsFormat(dateSpan.Value.Months)}",
        DateStatus.MonthUnknown => YearsFormat(dateSpan.Value.Years),
        DateStatus.YearApproximate when dateSpan.Value.Years == 0 => string.Empty,
        DateStatus.YearApproximate => string.Format(UIStrings.DateStatusYearApproximate_1, YearsFormat(dateSpan.Value.Years)),
        _ => string.Empty
      };
    }

    return string.IsNullOrWhiteSpace(ret) ? UIStrings.DateStatusUnknown : ret;
  }

  protected static string TwoLetterISOLanguageName =>
    System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

  protected static string DaysFormat(int days)
  {
    return TwoLetterISOLanguageName switch
    {
      _ when days == 0 => string.Empty,
      "ru" => RussianNumeralsDeclension(days, "{0} день", "{0} дня", "{0} дней"),
      _ => EnglishNumeralsDeclension(days, "{0} day", "{0} days")
    };
  }

  protected static string MonthsFormat(int months)
  {
    return TwoLetterISOLanguageName switch
    {
      _ when months == 0 => string.Empty,
      "ru" => RussianNumeralsDeclension(months, "{0} месяц", "{0} месяца", "{0} месяцев"),
      _ => EnglishNumeralsDeclension(months, "{0} month", "{0} months")
    };
  }

  protected static string YearsFormat(int years)
  {
    return TwoLetterISOLanguageName switch
    {
      _ when years == 0 => string.Empty,
      "ru" => RussianNumeralsDeclension(years, "{0} год", "{0} года", "{0} лет"),
      _ => EnglishNumeralsDeclension(years, "{0} year", "{0} years")
    };
  }

  protected static string EnglishNumeralsDeclension(int value, string single, string many)
  {
    return string.Format(value == 1 ? single : many, value);
  }

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