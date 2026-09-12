using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class DateFormatterTests
{
  private static DateFormatter Create(string fullFormat = "DD MMM YYYY", string shortFormat = "MMM YYYY", string calendar = "Gregorian")
  {
    var full = new Mock<ISettingEditor>();
    full.SetupGet(s => s.Value).Returns(fullFormat);

    var shortFmt = new Mock<ISettingEditor>();
    shortFmt.SetupGet(s => s.Value).Returns(shortFormat);

    var calendarSetting = new Mock<ISettingEditor>();
    calendarSetting.SetupGet(s => s.Value).Returns(calendar);

    return new DateFormatter(full.Object, shortFmt.Object, calendarSetting.Object);
  }

  private static void SetEn() => TestLanguage.Use(Language.EN);
  private static void SetRu() => TestLanguage.Use(Language.RU);
  private static void SetDe() => TestLanguage.Use(Language.DE);
  private static void SetEs() => TestLanguage.Use(Language.ES);
  private static void SetFr() => TestLanguage.Use(Language.FR);

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
  [InlineData(2, "February")]
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

  // Month names come from the satellite assembly rather than the neutral resource, so this also
  // covers that the de satellite is built and resolved through Language.Current.
  [Theory]
  [InlineData(1, "Januar")]
  [InlineData(3, "März")]
  [InlineData(10, "Oktober")]
  [InlineData(12, "Dezember")]
  public void DE_Months_MatchResourceStrings(int month, string expectedName)
  {
    SetDe();
    var date = Date.Create(2000, month, 0, DateStatus.DayUnknown);
    Create(shortFormat: "MMM YYYY").ToString(date).Should().StartWith(expectedName);
  }

  // Spanish month names are lowercase, and nothing in the formatter re-cases them.
  [Theory]
  [InlineData(1, "enero")]
  [InlineData(3, "marzo")]
  [InlineData(10, "octubre")]
  [InlineData(12, "diciembre")]
  public void ES_Months_MatchResourceStrings(int month, string expectedName)
  {
    SetEs();
    var date = Date.Create(2000, month, 0, DateStatus.DayUnknown);
    Create(shortFormat: "MMM YYYY").ToString(date).Should().StartWith(expectedName);
  }

  [Theory]
  [InlineData(1, "janvier")]
  [InlineData(3, "mars")]
  [InlineData(10, "octobre")]
  [InlineData(12, "décembre")]
  public void FR_Months_MatchResourceStrings(int month, string expectedName)
  {
    SetFr();
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

  [Fact]
  public void WellKnown_GregorianCalendarSelected_NeverShowsALabel()
  {
    SetEn();
    var date = Date.Create(2000, 1, 15, DateStatus.WellKnown);
    Create(calendar: "Gregorian").ToString(date).Should().Be("15 January 2000");
  }

  // Orthodox/Julian New Year: 14 Jan (Gregorian) is 1 Jan in the Julian calendar in this era.
  [Fact]
  public void WellKnown_JulianCalendar_ConvertsAndLabelsTheDate()
  {
    SetEn();
    var date = Date.Create(2000, 1, 14, DateStatus.WellKnown);
    Create(calendar: "Julian").ToString(date).Should().Be("01 January 2000 (Julian)");
  }

  [Fact]
  public void WellKnown_RU_JulianCalendar_MonthInGenitiveCase()
  {
    SetRu();
    var date = Date.Create(2000, 1, 14, DateStatus.WellKnown);
    Create(calendar: "Julian").ToString(date).Should().Be("01 января 2000 (Юлианский)");
  }

  // Documented on HebrewCalendar itself: 1 January 2001 is the sixth day of Tevet, 5761 AM.
  [Fact]
  public void WellKnown_HebrewCalendar_ConvertsAndLabelsTheDate()
  {
    SetEn();
    var date = Date.Create(2001, 1, 1, DateStatus.WellKnown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("06 Tevet 5761 (Hebrew)");
  }

  // Formats the same date under two languages in one test -- a static-field cache of the month
  // names (rather than a live UIStrings read) would pass this only if RU happened to run first.
  [Fact]
  public void WellKnown_HebrewCalendar_MonthNameIsLocalized()
  {
    var date = Date.Create(2001, 1, 1, DateStatus.WellKnown);

    SetEn();
    Create(calendar: "Hebrew").ToString(date).Should().Be("06 Tevet 5761 (Hebrew)");

    SetRu();
    Create(calendar: "Hebrew").ToString(date).Should().Be("06 тевета 5761 (Еврейский)");
  }

  // Tevet (above) happens to be spelled identically in en/de/es/fr, so it can't catch a satellite
  // resx shipping a wrong value under the right key -- Cheshvan differs across all four.
  [Fact]
  public void WellKnown_HebrewCalendar_DE_MonthNameMatchesResourceString()
  {
    SetDe();
    var date = Date.Create(2000, 11, 1, DateStatus.WellKnown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("03 Cheschwan 5761 (Hebräisch)");
  }

  [Fact]
  public void WellKnown_HebrewCalendar_ES_MonthNameMatchesResourceString()
  {
    SetEs();
    var date = Date.Create(2000, 11, 1, DateStatus.WellKnown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("03 Jeshván 5761 (Hebreo)");
  }

  [Fact]
  public void WellKnown_HebrewCalendar_FR_MonthNameMatchesResourceString()
  {
    SetFr();
    var date = Date.Create(2000, 11, 1, DateStatus.WellKnown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("03 Heshvan 5761 (Hébraïque)");
  }

  // Bourbon oracle from #386.
  [Fact]
  public void WellKnown_FrenchRepublicanCalendar_ConvertsAndLabelsTheDate()
  {
    SetEn();
    var date = Date.Create(1793, 1, 21, DateStatus.WellKnown);
    Create(calendar: "FrenchRepublican").ToString(date).Should().Be("02 Pluviôse 1 (French Republican)");
  }

  [Fact]
  public void WellKnown_FrenchRepublicanCalendar_RU_MonthNameIsLocalizedAndInGenitiveCase()
  {
    SetRu();
    var date = Date.Create(1793, 1, 21, DateStatus.WellKnown);
    Create(calendar: "FrenchRepublican").ToString(date).Should().Be("02 плювиоза 1 (Французский республиканский)");
  }

  // Prairial (fr/en/de) is Pradial in Spanish -- one of the few French Republican months whose
  // Spanish spelling actually differs, so this is the case that would catch a satellite resx
  // shipping the wrong value under the right key.
  [Fact]
  public void WellKnown_FrenchRepublicanCalendar_ES_MonthNameMatchesResourceString()
  {
    SetEs();
    var date = Date.Create(1793, 5, 20, DateStatus.WellKnown);
    Create(calendar: "FrenchRepublican").ToString(date).Should().Be("01 Pradial 1 (Republicano francés)");
  }

  // Adar I/Adar II end in a Latin numeral even in Russian resource strings ("Адар I") -- proves
  // MonthGenitiveRU leaves the Latin suffix alone instead of producing "адар iа".
  [Fact]
  public void WellKnown_HebrewLeapYear_RU_AdarNamesKeepTheirLatinNumeralUnmangled()
  {
    SetRu();
    Create(calendar: "Hebrew").ToString(Date.Create(2024, 2, 10, DateStatus.WellKnown)).Should().Be("01 адар i 5784 (Еврейский)");
    Create(calendar: "Hebrew").ToString(Date.Create(2024, 3, 11, DateStatus.WellKnown)).Should().Be("01 адар ii 5784 (Еврейский)");
  }

  [Fact]
  public void WellKnown_NonGregorianCalendar_OutOfRange_FallsBackToGregorianAndLabelsIt()
  {
    SetEn();
    var date = Date.Create(1850, 1, 1, DateStatus.WellKnown);
    Create(calendar: "FrenchRepublican").ToString(date).Should().Be("01 January 1850 (Gregorian)");
  }

  // A BC date always falls back to Gregorian, so it must show both the B.C. suffix and the
  // "(Gregorian)" label together -- proving neither one crowds out the other.
  [Fact]
  public void WellKnown_BeforeCommonEra_NonGregorianCalendar_FallsBackAndKeepsTheEraSuffix()
  {
    SetEn();
    var date = Date.Create(-440315, DateStatus.WellKnown);
    Create(fullFormat: "DD MMM YYYY", calendar: "Hebrew").ToString(date).Should().Be("15 March 44 B.C. (Gregorian)");
  }

  // hebcal.com converter, directly verified: Adar I and Adar II are distinct months (6 and 7) only in
  // a leap year -- exactly the index a wrong leap-year array would get wrong.
  [Fact]
  public void WellKnown_HebrewLeapYear_AdarIAndAdarIIAreDistinctMonths()
  {
    SetEn();
    Create(calendar: "Hebrew").ToString(Date.Create(2024, 2, 10, DateStatus.WellKnown)).Should().Be("01 Adar I 5784 (Hebrew)");
    Create(calendar: "Hebrew").ToString(Date.Create(2024, 3, 11, DateStatus.WellKnown)).Should().Be("01 Adar II 5784 (Hebrew)");
  }

  // hebcal.com converter, directly verified: in a common year month 7 is Nisan, not Adar II -- the
  // case that would catch collapsing the leap/common Hebrew month arrays into one.
  [Fact]
  public void WellKnown_HebrewCommonYear_MonthSevenIsNisanNotAdarII()
  {
    SetEn();
    var date = Date.Create(2001, 4, 17, DateStatus.WellKnown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("24 Nisan 5761 (Hebrew)");
  }

  // Elul is the last index of both HebrewMonths arrays -- 13 in a leap year, 12 in a common year --
  // exactly the boundary an off-by-one or an array/leap-flag mismatch throws IndexOutOfRangeException on.
  [Fact]
  public void WellKnown_HebrewLeapYear_MonthThirteenIsElul()
  {
    SetEn();
    var date = Date.Create(2024, 9, 4, DateStatus.WellKnown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("01 Elul 5784 (Hebrew)");
  }

  [Fact]
  public void WellKnown_HebrewCommonYear_MonthTwelveIsElul()
  {
    SetEn();
    var date = Date.Create(2001, 8, 20, DateStatus.WellKnown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("01 Elul 5761 (Hebrew)");
  }

  [Fact]
  public void DayUnknown_NonGregorianCalendar_StillLabelsAsGregorian()
  {
    SetEn();
    var date = Date.Create(2000, 3, 0, DateStatus.DayUnknown);
    Create(shortFormat: "MMM YYYY", calendar: "Hebrew").ToString(date).Should().Be("March 2000 (Gregorian)");
  }

  [Fact]
  public void MonthUnknown_NonGregorianCalendar_StillLabelsAsGregorian()
  {
    SetEn();
    var date = Date.Create(1985, 0, 0, DateStatus.MonthUnknown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("1985 (Gregorian)");
  }

  [Fact]
  public void YearApproximate_NonGregorianCalendar_StillLabelsAsGregorian()
  {
    SetEn();
    var date = Date.Create(1990, 0, 0, DateStatus.YearApproximate);
    Create(calendar: "Hebrew").ToString(date).Should().Be("about 1990 (Gregorian)");
  }

  [Fact]
  public void Unknown_NonGregorianCalendar_HasNoLabel()
  {
    SetEn();
    var date = Date.Create(0, 0, 0, DateStatus.Unknown);
    Create(calendar: "Hebrew").ToString(date).Should().Be("unknown (Hebrew)");
  }
}
