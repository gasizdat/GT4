using FluentAssertions;
using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
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

  // hebcal.com converter, directly verified: 1 Adar I 5784, the leap month that only exists because
  // 5784 is a leap year.
  [Fact]
  public void Hebrew_LeapYear_AdarI_IsMonthSix()
  {
    var date = Date.Create(2024, 2, 10, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Hebrew);

    result.Should().Be((DisplayCalendar.Hebrew, 5784, 6, 1));
  }

  // hebcal.com converter, directly verified: 1 Adar II 5784 -- month 7 only in a leap year, where a
  // common year has Nisan at month 7 instead. This is exactly the index a wrong leap-year array would
  // get wrong.
  [Fact]
  public void Hebrew_LeapYear_AdarII_IsMonthSeven()
  {
    var date = Date.Create(2024, 3, 11, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Hebrew);

    result.Should().Be((DisplayCalendar.Hebrew, 5784, 7, 1));
  }

  // hebcal.com converter, directly verified: 24 Nisan 5761, a common year, where month 7 is Nisan --
  // the case that would catch collapsing the leap/common Hebrew month arrays into one.
  [Fact]
  public void Hebrew_CommonYear_NisanIsMonthSeven()
  {
    var date = Date.Create(2001, 4, 17, DateStatus.WellKnown);

    var result = CalendarConversion.TryConvert(date, DisplayCalendar.Hebrew);

    result.Should().Be((DisplayCalendar.Hebrew, 5761, 7, 24));
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

  // Old Style -> New Style pairs from the historical record, one per century's gap: Britain's switch
  // (2 September 1752 O.S. was followed by 14 September N.S.), the Emancipation Manifesto, and the
  // October Revolution.
  [Theory]
  [InlineData(1752, 9, 2, 1752, 9, 13)]
  [InlineData(1861, 2, 19, 1861, 3, 3)]
  [InlineData(1917, 10, 25, 1917, 11, 7)]
  public void ToGregorian_Julian_MatchesTheHistoricalRecordAndRoundTrips(
    int year, int month, int day, int gYear, int gMonth, int gDay)
  {
    var gregorian = CalendarConversion.ToGregorian(DisplayCalendar.Julian, year, month, day);

    gregorian.Should().Be(Date.Create(gYear, gMonth, gDay, DateStatus.WellKnown));
    CalendarConversion.TryConvert(gregorian!.Value, DisplayCalendar.Julian)
      .Should().Be((DisplayCalendar.Julian, year, month, day));
  }

  // 1 March 1900 O.S. is already 13 days behind (the October Revolution gap), so the Julian-only leap
  // day before it lands one day earlier.
  [Fact]
  public void ToGregorian_Julian_LeapDayInACenturyYear_Exists()
  {
    var gregorian = CalendarConversion.ToGregorian(DisplayCalendar.Julian, 1900, 2, 29);

    gregorian.Should().Be(Date.Create(1900, 3, 13, DateStatus.WellKnown));
  }

  [Theory]
  [InlineData(1700)]
  [InlineData(1800)]
  [InlineData(1900)]
  public void DaysInMonth_FebruaryOfACenturyYear_IsALeapMonthOnlyInJulian(int year)
  {
    CalendarConversion.DaysInMonth(DisplayCalendar.Julian, year, 2).Should().Be(29);
    CalendarConversion.DaysInMonth(DisplayCalendar.Gregorian, year, 2).Should().Be(28);
  }

  [Fact]
  public void ToGregorian_Gregorian_LeapDayInACenturyYear_DoesNotExist()
  {
    CalendarConversion.ToGregorian(DisplayCalendar.Gregorian, 1900, 2, 29).Should().BeNull();
  }

  // The hebcal-verified and documented pairs above, reversed.
  [Theory]
  [InlineData(5761, 4, 6, 2001, 1, 1)]
  [InlineData(5784, 6, 1, 2024, 2, 10)]
  [InlineData(5784, 7, 1, 2024, 3, 11)]
  [InlineData(5761, 7, 24, 2001, 4, 17)]
  public void ToGregorian_Hebrew_MatchesTheKnownPairs(int year, int month, int day, int gYear, int gMonth, int gDay)
  {
    var gregorian = CalendarConversion.ToGregorian(DisplayCalendar.Hebrew, year, month, day);

    gregorian.Should().Be(Date.Create(gYear, gMonth, gDay, DateStatus.WellKnown));
  }

  [Theory]
  [InlineData(5761, 12)]
  [InlineData(5784, 13)]
  public void MonthsInYear_Hebrew_CountsAdarIIOnlyInALeapYear(int year, int months)
  {
    CalendarConversion.MonthsInYear(DisplayCalendar.Hebrew, year).Should().Be(months);
  }

  [Fact]
  public void ToGregorian_Hebrew_ThirteenthMonthOfACommonYear_DoesNotExist()
  {
    CalendarConversion.ToGregorian(DisplayCalendar.Hebrew, 5761, 13, 1).Should().BeNull();
  }

  // HebrewCalendar's range starts on 1 January 1583, partway through 5343.
  [Fact]
  public void ToGregorian_Hebrew_RangeStartsMidYear()
  {
    CalendarConversion.ToGregorian(DisplayCalendar.Hebrew, 5343, 4, 6).Should().BeNull();
    CalendarConversion.ToGregorian(DisplayCalendar.Hebrew, 5343, 4, 7).Should().NotBeNull();
  }

  [Fact]
  public void ToGregorian_FrenchRepublican_Epoch()
  {
    var gregorian = CalendarConversion.ToGregorian(DisplayCalendar.FrenchRepublican, 1, 1, 1);

    gregorian.Should().Be(Date.Create(1792, 9, 22, DateStatus.WellKnown));
  }

  // Year XIV began on 23 September 1805, so 10 Nivose is 31 December -- the calendar was abolished
  // the next day.
  [Fact]
  public void ToGregorian_FrenchRepublican_EndsAtAbolition()
  {
    CalendarConversion.ToGregorian(DisplayCalendar.FrenchRepublican, 14, 4, 10)
      .Should().Be(Date.Create(1805, 12, 31, DateStatus.WellKnown));
    CalendarConversion.ToGregorian(DisplayCalendar.FrenchRepublican, 14, 4, 11).Should().BeNull();
  }

  [Fact]
  public void ToGregorian_FrenchRepublican_ComplementaryDays_AreNotRepresentable()
  {
    CalendarConversion.ToGregorian(DisplayCalendar.FrenchRepublican, 3, 13, 1).Should().BeNull();
  }

  [Fact]
  public void FrenchRepublican_HasTwelveThirtyDayMonths()
  {
    CalendarConversion.MonthsInYear(DisplayCalendar.FrenchRepublican, 2).Should().Be(12);
    CalendarConversion.DaysInMonth(DisplayCalendar.FrenchRepublican, 2, 12).Should().Be(30);
  }

  [Theory]
  [InlineData(DisplayCalendar.Gregorian, 1, 9999)]
  [InlineData(DisplayCalendar.Julian, 1, 9999)]
  [InlineData(DisplayCalendar.Hebrew, 5343, 5999)]
  [InlineData(DisplayCalendar.FrenchRepublican, 1, 14)]
  public void YearRange_MatchesEachCalendarsLimits(DisplayCalendar calendar, int min, int max)
  {
    CalendarConversion.YearRange(calendar).Should().Be((min, max));
  }

  // Each must come back null rather than throw -- the month and day lookups throw outside the range.
  [Theory]
  [InlineData(DisplayCalendar.Gregorian, 0, 1, 1)]
  [InlineData(DisplayCalendar.Gregorian, 2000, 13, 1)]
  [InlineData(DisplayCalendar.Gregorian, 2000, 1, 0)]
  [InlineData(DisplayCalendar.Julian, 1, 1, 1)]
  [InlineData(DisplayCalendar.Julian, 9999, 12, 31)]
  [InlineData(DisplayCalendar.Hebrew, 5342, 1, 1)]
  [InlineData(DisplayCalendar.Hebrew, 6000, 1, 1)]
  [InlineData(DisplayCalendar.Hebrew, 5784, 0, 1)]
  [InlineData(DisplayCalendar.FrenchRepublican, 15, 1, 1)]
  public void ToGregorian_OutOfRange_IsNotRepresentable(DisplayCalendar calendar, int year, int month, int day)
  {
    CalendarConversion.ToGregorian(calendar, year, month, day).Should().BeNull();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("Sepia")]
  public void ToDisplayCalendar_UnrecognizedOrMissing_FallsBackToGregorian(string? value)
  {
    CalendarConversion.ToDisplayCalendar(value).Should().Be(DisplayCalendar.Gregorian);
  }

  [Fact]
  public void ToDisplayCalendar_RecognizesEveryDisplayCalendar()
  {
    CalendarConversion.ToDisplayCalendar(nameof(DisplayCalendar.Julian)).Should().Be(DisplayCalendar.Julian);
  }
}
