using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class PersonLifetimeMatcherTests
{
  private static Date Known(int year) => Date.Create(year, 1, 1, DateStatus.WellKnown);
  private static readonly Date Unknown = Date.Create(null, null, null, DateStatus.Unknown);

  [Theory]
  [InlineData(1950, false)]
  [InlineData(1960, true)]
  [InlineData(1980, true)]
  [InlineData(2000, true)]
  [InlineData(2010, false)]
  public void BothDatesKnown_MatchesOnlyWithinInclusiveRange(int year, bool expected)
  {
    PersonLifetimeMatcher.IsAliveInYear(Known(1960), Known(2000), year).Should().Be(expected);
  }

  [Fact]
  public void BirthKnown_Alive_MatchesWithinMaximumLifeExpectancyFromBirth()
  {
    var birth = Known(1950);

    PersonLifetimeMatcher.IsAliveInYear(birth, null, 1950).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(birth, null, 2000).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(birth, null, 2070).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(birth, null, 1949).Should().BeFalse();
    PersonLifetimeMatcher.IsAliveInYear(birth, null, 2071).Should().BeFalse();
  }

  [Fact]
  public void BirthKnown_KnownDeadUnknownDate_MatchesWithinMaximumLifeExpectancyFromBirth()
  {
    var birth = Known(1900);
    var unknownDeath = Unknown;

    PersonLifetimeMatcher.IsAliveInYear(birth, unknownDeath, 1900).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(birth, unknownDeath, 2000).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(birth, unknownDeath, 2020).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(birth, unknownDeath, 2021).Should().BeFalse();
    PersonLifetimeMatcher.IsAliveInYear(birth, unknownDeath, 1899).Should().BeFalse();
  }

  [Fact]
  public void UnknownBirth_KnownDeath_GuessesTwentyYearsBeforeDeath()
  {
    var death = Known(2000);

    PersonLifetimeMatcher.IsAliveInYear(Unknown, death, 2000).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(Unknown, death, 1980).Should().BeTrue();
    PersonLifetimeMatcher.IsAliveInYear(Unknown, death, 1979).Should().BeFalse();
    PersonLifetimeMatcher.IsAliveInYear(Unknown, death, 2001).Should().BeFalse();
  }

  [Theory]
  [InlineData(1)]
  [InlineData(1900)]
  [InlineData(3000)]
  public void UnknownBirth_Alive_NeverMatches(int year)
  {
    PersonLifetimeMatcher.IsAliveInYear(Unknown, null, year).Should().BeFalse();
  }

  [Theory]
  [InlineData(1)]
  [InlineData(1900)]
  [InlineData(3000)]
  public void UnknownBirth_KnownDeadUnknownDate_NeverMatches(int year)
  {
    PersonLifetimeMatcher.IsAliveInYear(Unknown, Unknown, year).Should().BeFalse();
  }
}
