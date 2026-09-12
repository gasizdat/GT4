using FluentAssertions;
using GT4.Core.Utils.Calendars;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class FrenchRepublicanCalendarTests
{
  [Theory]
  // Louis XVI's execution: 21 January 1793 -> 2 Pluviose An I, the epoch.
  [InlineData(1793, 1, 21, 1, 5, 2)]
  // Marie Antoinette's execution: 16 October 1793 -> 25 Vendemiaire An II.
  [InlineData(1793, 10, 16, 2, 1, 25)]
  // An III's sextile epoch shifts An IV's start to 23 (not 22) September -- exactly where an
  // off-by-one in the epoch-window lookup would hide.
  [InlineData(1795, 9, 23, 4, 1, 1)]
  public void TryFromGregorian_MatchesTheKnownOracle(int gYear, int gMonth, int gDay, int year, int month, int day)
  {
    FrenchRepublicanCalendar.TryFromGregorian(new DateTime(gYear, gMonth, gDay), out var republican).Should().BeTrue();

    republican.Should().Be((year, month, day));
  }

  // The day before the An III->IV epoch shift is a complementary day of An III, not the last day of
  // An III's twelfth month.
  [Fact]
  public void TryFromGregorian_ComplementaryDay_IsNotRepresentable()
  {
    FrenchRepublicanCalendar.TryFromGregorian(new DateTime(1795, 9, 22), out _).Should().BeFalse();
  }

  [Fact]
  public void TryFromGregorian_BeforeTheCalendarExisted_IsNotRepresentable()
  {
    FrenchRepublicanCalendar.TryFromGregorian(new DateTime(1792, 9, 21), out _).Should().BeFalse();
  }

  [Fact]
  public void TryFromGregorian_AfterAbolition_IsNotRepresentable()
  {
    FrenchRepublicanCalendar.TryFromGregorian(new DateTime(1806, 1, 1), out _).Should().BeFalse();
  }

  [Fact]
  public void ToGregorian_RoundTripsWithTryFromGregorian()
  {
    var gregorian = FrenchRepublicanCalendar.ToGregorian(2, 1, 25);

    gregorian.Should().Be(new DateTime(1793, 10, 16));
  }

  [Theory]
  [InlineData(0, 1, 1)]
  [InlineData(15, 1, 1)]
  [InlineData(1, 13, 1)]
  [InlineData(1, 1, 31)]
  public void ToGregorian_OutOfRange_ReturnsNull(int year, int month, int day)
  {
    FrenchRepublicanCalendar.ToGregorian(year, month, day).Should().BeNull();
  }
}
