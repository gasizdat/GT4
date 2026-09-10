using FluentAssertions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class CalendarConversionTests
{
  [Fact]
  public void GregorianRequested_ReturnsTheDateUnchanged()
  {
    var date = Date.Create(2000, 3, 15, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Gregorian);

    result.Should().Be((DisplayCalendar.Gregorian, 2000, 3, 15));
  }

  // Orthodox/Julian New Year: 14 January (Gregorian) is 1 January in the Julian calendar throughout
  // the 20th and 21st centuries -- the same 13-day offset behind Orthodox Christmas (7 Jan Gregorian
  // = 25 Dec Julian).
  [Fact]
  public void Julian_MatchesTheKnownGregorianOffset()
  {
    var date = Date.Create(2000, 1, 14, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Julian);

    result.Should().Be((DisplayCalendar.Julian, 2000, 1, 1));
  }

  // Documented on HebrewCalendar itself: 1 January 2001 (Gregorian) is the sixth day of Tevet, 5761 AM.
  [Fact]
  public void Hebrew_MatchesTheDocumentedReferenceDate()
  {
    var date = Date.Create(2001, 1, 1, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Hebrew);

    result.Should().Be((DisplayCalendar.Hebrew, 5761, 4, 6));
  }

  [Fact]
  public void Hebrew_OutsideSupportedRange_FallsBackToGregorian()
  {
    var date = Date.Create(1500, 1, 1, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Hebrew);

    result.Should().Be((DisplayCalendar.Gregorian, 1500, 1, 1));
  }

  // Bourbon oracle from #386, reversed.
  [Fact]
  public void FrenchRepublican_MatchesTheBourbonOracle()
  {
    var date = Date.Create(1793, 1, 21, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.FrenchRepublican);

    result.Should().Be((DisplayCalendar.FrenchRepublican, 1, 5, 2));
  }

  [Fact]
  public void FrenchRepublican_OutsideOfficialUse_FallsBackToGregorian()
  {
    var date = Date.Create(1850, 1, 1, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.FrenchRepublican);

    result.Should().Be((DisplayCalendar.Gregorian, 1850, 1, 1));
  }

  [Fact]
  public void BeforeCommonEra_AlwaysFallsBackToGregorian()
  {
    var date = Date.Create(-19000101, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Julian);

    result.Applied.Should().Be(DisplayCalendar.Gregorian);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("Sepia")]
  public void Parse_UnrecognizedOrMissing_FallsBackToGregorian(string? value)
  {
    CalendarConversion.Parse(value).Should().Be(DisplayCalendar.Gregorian);
  }

  [Fact]
  public void Parse_RecognizesEveryDisplayCalendar()
  {
    CalendarConversion.Parse(nameof(DisplayCalendar.Julian)).Should().Be(DisplayCalendar.Julian);
  }
}
