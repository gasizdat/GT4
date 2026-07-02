using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.View.Tests;

public class DateSpanFormatterTests
{
  private readonly DateSpanFormatter _formatter = new();

  private static void SetEn() => Language.Current = Language.EN;
  private static void SetRu() => Language.Current = Language.RU;

  private static DateSpan WellKnown(int years, int months, int days) =>
    new(years, months, days, DateStatus.WellKnown);

  private static DateSpan DayUnknown(int years, int months) =>
    new(years, months, 0, DateStatus.DayUnknown);

  private static DateSpan MonthUnknown(int years) =>
    new(years, 0, 0, DateStatus.MonthUnknown);

  private static DateSpan YearApproximate(int years) =>
    new(years, 0, 0, DateStatus.YearApproximate);

  [Fact]
  public void NullSpan_ReturnsUnknown()
  {
    SetEn();
    _formatter.ToString(null).Should().Be("unknown");
  }

  [Fact]
  public void RU_NullSpan_ReturnsUnknown()
  {
    SetRu();
    _formatter.ToString(null).Should().Be("неизвестно");
  }

  // English year declension: singular "year", plural "years"
  [Theory]
  [InlineData(1, "1 year")]
  [InlineData(2, "2 years")]
  [InlineData(5, "5 years")]
  [InlineData(11, "11 years")]
  [InlineData(21, "21 years")]
  [InlineData(100, "100 years")]
  public void EN_YearDeclension(int years, string expectedYearPart)
  {
    SetEn();
    var span = MonthUnknown(years);
    _formatter.ToString(span).Should().Be(expectedYearPart);
  }

  // Russian year declension: 1 → год, 2-4 → года, 5+ → лет, 11-20 always → лет
  [Theory]
  [InlineData(1, "1 год")]
  [InlineData(2, "2 года")]
  [InlineData(3, "3 года")]
  [InlineData(4, "4 года")]
  [InlineData(5, "5 лет")]
  [InlineData(10, "10 лет")]
  [InlineData(11, "11 лет")]
  [InlineData(12, "12 лет")]
  [InlineData(20, "20 лет")]
  [InlineData(21, "21 год")]
  [InlineData(22, "22 года")]
  [InlineData(25, "25 лет")]
  [InlineData(100, "100 лет")]
  [InlineData(101, "101 год")]
  [InlineData(111, "111 лет")]
  public void RU_YearDeclension(int years, string expectedYearPart)
  {
    SetRu();
    var span = MonthUnknown(years);
    _formatter.ToString(span).Should().Be(expectedYearPart);
  }

  // Month declension tests — use Trim() because the formatter does not remove the
  // leading space that comes from an empty years component in the DayUnknown template.
  [Theory]
  [InlineData(1, "1 month")]
  [InlineData(2, "2 months")]
  [InlineData(12, "12 months")]
  public void EN_MonthDeclension(int months, string expectedMonthPart)
  {
    SetEn();
    var span = DayUnknown(0, months);
    _formatter.ToString(span).Trim().Should().Be(expectedMonthPart);
  }

  [Theory]
  [InlineData(1, "1 месяц")]
  [InlineData(2, "2 месяца")]
  [InlineData(4, "4 месяца")]
  [InlineData(5, "5 месяцев")]
  [InlineData(11, "11 месяцев")]
  [InlineData(21, "21 месяц")]
  public void RU_MonthDeclension(int months, string expectedMonthPart)
  {
    SetRu();
    var span = DayUnknown(0, months);
    _formatter.ToString(span).Trim().Should().Be(expectedMonthPart);
  }

  // Day declension tests — use Trim() because WellKnown(0, 0, n) produces two leading spaces.
  [Theory]
  [InlineData(1, "1 day")]
  [InlineData(2, "2 days")]
  [InlineData(30, "30 days")]
  public void EN_DayDeclension(int days, string expectedDayPart)
  {
    SetEn();
    var span = WellKnown(0, 0, days);
    _formatter.ToString(span).Trim().Should().Be(expectedDayPart);
  }

  [Theory]
  [InlineData(1, "1 день")]
  [InlineData(2, "2 дня")]
  [InlineData(5, "5 дней")]
  [InlineData(11, "11 дней")]
  [InlineData(21, "21 день")]
  public void RU_DayDeclension(int days, string expectedDayPart)
  {
    SetRu();
    var span = WellKnown(0, 0, days);
    _formatter.ToString(span).Trim().Should().Be(expectedDayPart);
  }

  [Fact]
  public void EN_WellKnown_CombinesYearsMonthsDays()
  {
    SetEn();
    var span = WellKnown(25, 3, 15);
    _formatter.ToString(span).Should().Be("25 years 3 months 15 days");
  }

  [Fact]
  public void RU_WellKnown_CombinesYearsMonthsDays()
  {
    SetRu();
    var span = WellKnown(2, 1, 1);
    _formatter.ToString(span).Should().Be("2 года 1 месяц 1 день");
  }

  [Fact]
  public void EN_DayUnknown_CombinesYearsAndMonths()
  {
    SetEn();
    var span = DayUnknown(5, 6);
    _formatter.ToString(span).Should().Be("5 years 6 months");
  }

  [Fact]
  public void EN_ZeroYears_YearTextOmittedInOutput()
  {
    SetEn();
    var span = DayUnknown(0, 3);
    // The formatter keeps the template space: "" + " " + "3 months" = " 3 months"
    _formatter.ToString(span).Should().Be(" 3 months");
  }

  [Fact]
  public void EN_ZeroMonths_MonthTextOmittedInOutput()
  {
    SetEn();
    var span = DayUnknown(2, 0);
    // The formatter keeps the template space: "2 years" + " " + "" = "2 years "
    _formatter.ToString(span).Should().Be("2 years ");
  }

  [Fact]
  public void YearApproximate_NonZeroYear_ReturnsApproximateFormat()
  {
    SetEn();
    var span = YearApproximate(30);
    _formatter.ToString(span).Should().Be("about 30 years");
  }

  [Fact]
  public void YearApproximate_ZeroYears_ReturnsUnknown()
  {
    SetEn();
    var span = YearApproximate(0);
    _formatter.ToString(span).Should().Be("unknown");
  }

  [Fact]
  public void AllZeros_WellKnown_ReturnsUnknown()
  {
    SetEn();
    var span = WellKnown(0, 0, 0);
    _formatter.ToString(span).Should().Be("unknown");
  }
}
