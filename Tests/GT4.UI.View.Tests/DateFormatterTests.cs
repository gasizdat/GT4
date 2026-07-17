using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class DateFormatterTests
{
  private static DateFormatter Create(string fullFormat = "DD MMM YYYY", string shortFormat = "MMM YYYY")
  {
    var full = new Mock<ISettingEditor>();
    full.SetupGet(s => s.Value).Returns(fullFormat);

    var shortFmt = new Mock<ISettingEditor>();
    shortFmt.SetupGet(s => s.Value).Returns(shortFormat);

    return new DateFormatter(full.Object, shortFmt.Object);
  }

  private static void SetEn() => Language.Current = Language.EN;
  private static void SetRu() => Language.Current = Language.RU;

  [Fact]
  public void NullDate_ReturnsNotDefined()
  {
    SetEn();
    Create().ToString(null).Should().Be("not defined");
  }

  [Fact]
  public void RU_NullDate_ReturnsNotDefined()
  {
    SetRu();
    Create().ToString(null).Should().Be("не задано");
  }

  [Fact]
  public void WellKnown_FullDateRendered()
  {
    SetEn();
    var date = Date.Create(2000, 1, 15, DateStatus.WellKnown);
    Create(fullFormat: "DD MMM YYYY").ToString(date).Should().Be("15 January 2000");
  }

  [Fact]
  public void WellKnown_NumericMonthFormat()
  {
    SetEn();
    var date = Date.Create(2000, 3, 5, DateStatus.WellKnown);
    Create(fullFormat: "DD MM YYYY").ToString(date).Should().Be("05 03 2000");
  }

  [Fact]
  public void WellKnown_RU_MonthInGenitiveCase()
  {
    SetRu();
    var date = Date.Create(2000, 1, 15, DateStatus.WellKnown);
    // RU Jan = "Январь" → lower → "январь" → genitive → "января"
    Create(fullFormat: "DD MMM YYYY").ToString(date).Should().Be("15 января 2000");
  }

  [Fact]
  public void DayUnknown_ShortDateRendered()
  {
    SetEn();
    var date = Date.Create(2000, 3, 0, DateStatus.DayUnknown);
    Create(shortFormat: "MMM YYYY").ToString(date).Should().Be("March 2000");
  }

  [Fact]
  public void DayUnknown_RU_MonthNotInGenitiveCase()
  {
    SetRu();
    var date = Date.Create(2000, 1, 0, DateStatus.DayUnknown);
    // RU Jan = "Январь" → lower → "январь" (no genitive for DayUnknown)
    Create(shortFormat: "MMM YYYY").ToString(date).Should().Be("январь 2000");
  }

  [Fact]
  public void MonthUnknown_YearOnly()
  {
    SetEn();
    var date = Date.Create(1985, 0, 0, DateStatus.MonthUnknown);
    Create().ToString(date).Should().Be("1985");
  }

  [Fact]
  public void YearApproximate_ReturnsAboutYear()
  {
    SetEn();
    var date = Date.Create(1990, 0, 0, DateStatus.YearApproximate);
    Create().ToString(date).Should().Be("about 1990");
  }

  [Fact]
  public void RU_YearApproximate_ReturnsLocalizedString()
  {
    SetRu();
    var date = Date.Create(1990, 0, 0, DateStatus.YearApproximate);
    Create().ToString(date).Should().Be("около 1990");
  }

  [Fact]
  public void Unknown_ReturnsUnknownString()
  {
    SetEn();
    var date = Date.Create(0, 0, 0, DateStatus.Unknown);
    Create().ToString(date).Should().Be("unknown");
  }

  [Fact]
  public void RU_Unknown_ReturnsLocalizedString()
  {
    SetRu();
    var date = Date.Create(0, 0, 0, DateStatus.Unknown);
    Create().ToString(date).Should().Be("неизвестно");
  }

  [Theory]
  [InlineData(1, "January")]
  [InlineData(2, "Fabruary")]   // intentional typo preserved from resource
  [InlineData(3, "March")]
  [InlineData(4, "April")]
  [InlineData(5, "May")]
  [InlineData(6, "June")]
  [InlineData(7, "July")]
  [InlineData(8, "August")]
  [InlineData(9, "September")]
  [InlineData(10, "October")]
  [InlineData(11, "November")]
  [InlineData(12, "December")]
  public void EN_AllMonths_MatchResourceStrings(int month, string expectedName)
  {
    SetEn();
    var date = Date.Create(2000, month, 0, DateStatus.DayUnknown);
    Create(shortFormat: "MMM YYYY").ToString(date).Should().StartWith(expectedName);
  }

  [Fact]
  public void BeforeCommonEra_YearGetsSuffix()
  {
    SetEn();
    var date = Date.Create(-1000000, DateStatus.MonthUnknown);
    Create().ToString(date).Should().Be("100 B.C.");
  }

  [Fact]
  public void RU_BeforeCommonEra_YearGetsLocalizedSuffix()
  {
    SetRu();
    var date = Date.Create(-1000000, DateStatus.MonthUnknown);
    Create().ToString(date).Should().Be("100 до н. э.");
  }

  [Fact]
  public void BeforeCommonEra_FullDate_SuffixFollowsYear()
  {
    SetEn();
    var date = Date.Create(-440315, DateStatus.WellKnown);
    Create(fullFormat: "DD MMM YYYY").ToString(date).Should().Be("15 March 44 B.C.");
  }

  [Theory]
  [InlineData(5, "5")]
  [InlineData(50, "50")]
  [InlineData(100, "100")]
  [InlineData(1850, "1850")]
  public void WellKnown_YearNotZeroPadded(int year, string expected)
  {
    SetEn();
    var date = Date.Create(year, 6, 1, DateStatus.WellKnown);
    Create(fullFormat: "YYYY").ToString(date).Should().Be(expected);
  }
}
