using FluentAssertions;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class DateNormalizerTests
{
  [Theory]
  [InlineData(1, 1)]
  [InlineData(6, 6)]
  [InlineData(12, 12)]
  public void NormalizeMonth_ValidMonth_ReturnsUnchanged(int month, int expected) =>
    DateNormalizer.NormalizeMonth(month).Should().Be(expected);

  [Theory]
  [InlineData(0, 1)]
  [InlineData(-1, 1)]
  [InlineData(-99, 1)]
  public void NormalizeMonth_BelowMin_ReturnsOne(int month, int expected) =>
    DateNormalizer.NormalizeMonth(month).Should().Be(expected);

  [Theory]
  [InlineData(13, 12)]
  [InlineData(99, 12)]
  public void NormalizeMonth_AboveMax_ReturnsTwelve(int month, int expected) =>
    DateNormalizer.NormalizeMonth(month).Should().Be(expected);

  [Theory]
  [InlineData(2000)]  // divisible by 400
  [InlineData(1600)]
  [InlineData(1996)]  // divisible by 4, not 100
  [InlineData(2024)]
  [InlineData(2004)]
  public void IsLeapYear_LeapYear_ReturnsTrue(int year) =>
    DateNormalizer.IsLeapYear(year).Should().BeTrue();

  [Theory]
  [InlineData(1900)]  // divisible by 100, not 400
  [InlineData(1800)]
  [InlineData(1700)]
  [InlineData(1997)]  // not divisible by 4
  [InlineData(2023)]
  [InlineData(2100)]
  public void IsLeapYear_NonLeapYear_ReturnsFalse(int year) =>
    DateNormalizer.IsLeapYear(year).Should().BeFalse();

  [Theory]
  [InlineData(1, 31)]   // January
  [InlineData(3, 31)]   // March
  [InlineData(5, 31)]   // May
  [InlineData(7, 31)]   // July
  [InlineData(8, 31)]   // August
  [InlineData(10, 31)]  // October
  [InlineData(12, 31)]  // December
  public void NormalizeDay_LastDayOf31DayMonth_ReturnsUnchanged(int month, int day) =>
    DateNormalizer.NormalizeDay(2023, month, day).Should().Be(day);

  [Theory]
  [InlineData(4, 30)]   // April
  [InlineData(6, 30)]   // June
  [InlineData(9, 30)]   // September
  [InlineData(11, 30)]  // November
  public void NormalizeDay_LastDayOf30DayMonth_ReturnsUnchanged(int month, int day) =>
    DateNormalizer.NormalizeDay(2023, month, day).Should().Be(day);

  [Theory]
  [InlineData(1, 32, 31)]
  [InlineData(3, 32, 31)]
  [InlineData(4, 31, 30)]
  [InlineData(6, 31, 30)]
  public void NormalizeDay_DayExceedsMonthLength_ClampsToMonthMax(int month, int day, int expected) =>
    DateNormalizer.NormalizeDay(2023, month, day).Should().Be(expected);

  [Theory]
  [InlineData(1, 0, 1)]
  [InlineData(6, -5, 1)]
  public void NormalizeDay_DayBelowOne_ReturnsOne(int month, int day, int expected) =>
    DateNormalizer.NormalizeDay(2023, month, day).Should().Be(expected);

  [Fact]
  public void NormalizeDay_February28_NonLeapYear_ReturnsUnchanged() =>
    DateNormalizer.NormalizeDay(2023, month: 2, day: 28).Should().Be(28);

  [Fact]
  public void NormalizeDay_February29_NonLeapYear_ClampsTo28() =>
    DateNormalizer.NormalizeDay(2023, month: 2, day: 29).Should().Be(28);

  [Fact]
  public void NormalizeDay_February29_LeapYear_ReturnsUnchanged() =>
    DateNormalizer.NormalizeDay(2024, month: 2, day: 29).Should().Be(29);

  [Fact]
  public void NormalizeDay_February30_LeapYear_ClampsTo29() =>
    DateNormalizer.NormalizeDay(2024, month: 2, day: 30).Should().Be(29);

  [Fact]
  public void NormalizeDay_February29_Year2000_ReturnsUnchanged() =>
    DateNormalizer.NormalizeDay(2000, month: 2, day: 29).Should().Be(29);

  [Fact]
  public void NormalizeDay_February29_Year1900_ClampsTo28() =>
    DateNormalizer.NormalizeDay(1900, month: 2, day: 29).Should().Be(28);
}
