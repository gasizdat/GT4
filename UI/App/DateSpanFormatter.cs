using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI;

public class DateSpanFormatter : IDateSpanFormatter
{
  public string ToString(DateSpan? dateSpan)
  {
    // TODO use configuration

    if (dateSpan.HasValue)
    {
      switch (dateSpan.Value.Status)
      {
        case DateStatus.WellKnown:
          return $"{YearsFormat(dateSpan.Value.Years)} {MonthsFormat(dateSpan.Value.Months)} {DaysFormat(dateSpan.Value.Days)}";
        case DateStatus.DayUnknown:
          return $"{YearsFormat(dateSpan.Value.Years)} {MonthsFormat(dateSpan.Value.Months)}";
        case DateStatus.MonthUnknown:
          return $"{YearsFormat(dateSpan.Value.Years)}";
        case DateStatus.YearApproximate:
          return string.Format(UIStrings.DateStatusYearApproximate_1, YearsFormat(dateSpan.Value.Years));
      }
    }

    return UIStrings.DateStatusUnknown;
  }

  protected static string TwoLetterISOLanguageName =>
    System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

  protected static string DaysFormat(int days)
  {
    switch (TwoLetterISOLanguageName)
    {
      case "ru":
        return RussianNumeralsDeclension(days, "{0} день", "{0} дня", "{0} дней");
      case "en":
      default:
        return EnglishNumeralsDeclension(days, "{0} day", "{0} days");
    }
  }

  protected static string MonthsFormat(int days)
  {
    switch (TwoLetterISOLanguageName)
    {
      case "ru":
        return RussianNumeralsDeclension(days, "{0} месяц", "{0} месяца", "{0} месяцев");
      case "en":
      default:
        return EnglishNumeralsDeclension(days, "{0} month", "{0} months");
    }
  }

  protected static string YearsFormat(int days)
  {
    switch (TwoLetterISOLanguageName)
    {
      case "ru":
        return RussianNumeralsDeclension(days, "{0} год", "{0} года", "{0} лет");
      case "en":
      default:
        return EnglishNumeralsDeclension(days, "{0} year", "{0} years");
    }
  }

  protected static string EnglishNumeralsDeclension(int value, string single, string many)
  {
    return string.Format(value == 1 ? single : many, value);
  }

  protected static string RussianNumeralsDeclension(int value, string single, string several, string many)
  {
    int lastTwoDigits = value % 100;
    if (lastTwoDigits >= 11 && lastTwoDigits <= 20)
    {
      return many;
    }

    int lastDigit = value % 10;
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