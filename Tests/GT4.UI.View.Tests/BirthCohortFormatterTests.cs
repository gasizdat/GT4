using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.View.Tests;

public class BirthCohortFormatterTests
{
  private readonly BirthCohortFormatter _formatter = new();

  private static void SetEn() => TestLanguage.Use(Language.EN);

  [Theory]
  [InlineData(1882, null)]
  [InlineData(1883, "Lost Generation")]
  [InlineData(1900, "Lost Generation")]
  [InlineData(1901, "Greatest Generation")]
  [InlineData(1927, "Greatest Generation")]
  [InlineData(1928, "Silent Generation")]
  [InlineData(1945, "Silent Generation")]
  [InlineData(1946, "Baby Boomers")]
  [InlineData(1964, "Baby Boomers")]
  [InlineData(1965, "Generation X")]
  [InlineData(1980, "Generation X")]
  [InlineData(1981, "Millennials")]
  [InlineData(1996, "Millennials")]
  [InlineData(1997, "Generation Z")]
  [InlineData(2012, "Generation Z")]
  [InlineData(2013, "Generation Alpha")]
  [InlineData(2024, "Generation Alpha")]
  [InlineData(2025, "Generation Beta")]
  public void EN_EachBoundary_FallsInItsCohort(int year, string? expected)
  {
    SetEn();
    var birthDate = Date.Create(year, 6, 15, DateStatus.WellKnown);
    _formatter.ToString(birthDate).Should().Be(expected);
  }

  [Fact]
  public void EN_ApproximateYear_UsesThatYear()
  {
    SetEn();
    var birthDate = Date.Create(1950, null, null, DateStatus.YearApproximate);
    _formatter.ToString(birthDate).Should().Be("Baby Boomers");
  }

  [Fact]
  public void UnknownDate_HasNoCohort()
  {
    var birthDate = Date.Create(null, null, null, DateStatus.Unknown);
    _formatter.ToString(birthDate).Should().BeNull();
  }

  [Fact]
  public void BceYear_HasNoCohort()
  {
    var birthDate = Date.Create(-1950, null, null, DateStatus.MonthUnknown);
    _formatter.ToString(birthDate).Should().BeNull();
  }
}
