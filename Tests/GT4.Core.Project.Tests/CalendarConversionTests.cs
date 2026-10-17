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

  // Historical O.S. -> N.S. pairs, one per century's gap: Britain's switch (its 2 September was
  // followed by the 14th), the Emancipation Manifesto, the October Revolution.
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

  // The 13-day gap already applies here: 1 March 1900 O.S. is 14 March N.S.
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

  // Documented on HebrewCalendar and verified on hebcal.com.
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

  // 5784 is a leap year, 5785 and 5786 are common.
  [Theory]
  [InlineData(1, 5784, 5785, 1)]
  [InlineData(6, 5784, 5785, 6)]
  [InlineData(7, 5784, 5785, 6)]
  [InlineData(8, 5784, 5785, 7)]
  [InlineData(13, 5784, 5785, 12)]
  [InlineData(5, 5785, 5784, 5)]
  [InlineData(6, 5785, 5784, 7)]
  [InlineData(7, 5785, 5784, 8)]
  [InlineData(7, 5785, 5786, 7)]
  public void HebrewMonthInYear_KeepsTheMonthByName(int month, int fromYear, int toYear, int expected)
  {
    CalendarConversion.HebrewMonthInYear(month, fromYear, toYear).Should().Be(expected);
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

  [Theory]
  [InlineData(DisplayCalendar.Gregorian)]
  [InlineData(DisplayCalendar.Julian)]
  [InlineData(DisplayCalendar.Hebrew)]
  [InlineData(DisplayCalendar.FrenchRepublican)]
  public void YearRange_IsAcceptedByTheMonthAndDayLookups(DisplayCalendar calendar)
  {
    var (min, max) = CalendarConversion.YearRange(calendar);

    foreach (var year in new[] { min, max })
    {
      var months = CalendarConversion.MonthsInYear(calendar, year);
      CalendarConversion.DaysInMonth(calendar, year, months).Should().BePositive();
    }
  }

  [Fact]
  public void ToGregorian_Gregorian_ReturnsTheDateUnchanged()
  {
    var gregorian = CalendarConversion.ToGregorian(DisplayCalendar.Gregorian, 2000, 3, 15);

    gregorian.Should().Be(Date.Create(2000, 3, 15, DateStatus.WellKnown));
  }

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
