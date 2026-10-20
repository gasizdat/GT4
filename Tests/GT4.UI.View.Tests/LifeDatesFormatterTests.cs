using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class LifeDatesFormatterTests
{
  private static LifeDatesFormatter Create() =>
    new(Mock.Of<IDateFormatter>(), Mock.Of<IDateSpanFormatter>());

  private static Person MakePerson(Date birth, Date? death) => new(1, birth, death, BiologicalSex.Unknown);

  private static Date Year(int year, DateStatus status = DateStatus.MonthUnknown) => Date.Create(year, null, null, status);

  private static string Years(Person person, DisplayCalendar calendar = DisplayCalendar.Gregorian)
  {
    var runs = Create().ToYears(person, calendar);
    return string.Concat(runs.Select(run => run.Text));
  }

  private static readonly Date UnknownDate = Date.Create(0, DateStatus.Unknown);

  [Fact]
  public void ToYears_GivesBothYears()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Year(1820), Year(1885));

    Years(person).Should().Be("1820 - † 1885");
  }

  [Fact]
  public void ToYears_TakesOnlyTheYearOfAWholeDate()
  {
    TestLanguage.Use(Language.EN);

    var birth = Date.Create(1820, 5, 14, DateStatus.WellKnown);
    var person = MakePerson(birth, null);

    Years(person).Should().Be("1820");
  }

  // A segment has no room for "about 1840".
  [Fact]
  public void ToYears_MarksAnApproximateYear()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Year(1840, DateStatus.YearApproximate), null);

    Years(person).Should().Be("~1840");
  }

  [Fact]
  public void ToYears_MarksAYearBeforeChrist()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Date.Create(-630000, DateStatus.MonthUnknown), Year(14));

    Years(person).Should().Be("63 B.C. - † 14");
  }

  [Fact]
  public void ToYears_KeepsTheDeathMarkForAnUnknownDeathDate()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Year(1820), UnknownDate);

    Years(person).Should().Be("1820 - † ");
  }

  [Fact]
  public void ToYears_GivesOnlyTheDeathWhenTheBirthIsUnknown()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(UnknownDate, Year(1885));

    Years(person).Should().Be("† 1885");
  }

  [Fact]
  public void ToYears_GivesNothingWhenNoYearIsKnown()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(UnknownDate, null);

    Create().ToYears(person, DisplayCalendar.Gregorian).Should().BeEmpty();
  }

  [Fact]
  public void ToYears_MarksNoYearUnderGregorian()
  {
    TestLanguage.Use(Language.EN);

    var birth = Date.Create(-630000, DateStatus.MonthUnknown);
    var death = Date.Create(14, 8, 19, DateStatus.WellKnown);
    var person = MakePerson(birth, death);

    Create().ToYears(person, DisplayCalendar.Gregorian).Should().NotContain(run => run.IsFallback);
  }

  [Theory]
  [InlineData(1900, 1, 5, DisplayCalendar.Julian, "1899")]
  [InlineData(1820, 5, 14, DisplayCalendar.Hebrew, "5580")]
  [InlineData(1800, 5, 14, DisplayCalendar.FrenchRepublican, "8")]
  public void ToYears_ConvertsAWholeDatesYear(int year, int month, int day, DisplayCalendar calendar, string expected)
  {
    TestLanguage.Use(Language.EN);

    var birth = Date.Create(year, month, day, DateStatus.WellKnown);
    var person = MakePerson(birth, null);

    Create().ToYears(person, calendar).Should().Equal((expected, false));
  }

  // The mark follows the span across the calendar's new year, not only a year recorded as approximate.
  [Theory]
  [InlineData(1861, 3, DateStatus.DayUnknown, DisplayCalendar.Hebrew, "5621")]
  [InlineData(1990, null, DateStatus.MonthUnknown, DisplayCalendar.Julian, "~1990")]
  [InlineData(1990, null, DateStatus.YearApproximate, DisplayCalendar.Hebrew, "~5750")]
  public void ToYears_ConvertsAPartialDatesYear(int year, int? month, DateStatus status, DisplayCalendar calendar, string expected)
  {
    TestLanguage.Use(Language.EN);

    var birth = Date.Create(year, month, null, status);
    var person = MakePerson(birth, null);

    Create().ToYears(person, calendar).Should().Equal((expected, false));
  }

  [Theory]
  [InlineData(1575, DisplayCalendar.Hebrew, "1575")]
  [InlineData(1810, DisplayCalendar.FrenchRepublican, "1810")]
  [InlineData(-63, DisplayCalendar.Julian, "63 B.C.")]
  public void ToYears_LeavesAYearTheCalendarCannotHoldInGregorian(int year, DisplayCalendar calendar, string expected)
  {
    TestLanguage.Use(Language.EN);

    var birth = Date.Create(year * 10000, DateStatus.MonthUnknown);
    var person = MakePerson(birth, null);

    Create().ToYears(person, calendar).Should().Equal((expected, true));
  }

  [Fact]
  public void ToYears_MarksOnlyTheYearThatStaysGregorian()
  {
    TestLanguage.Use(Language.EN);

    var death = Date.Create(1640, 3, 10, DateStatus.WellKnown);
    var person = MakePerson(Year(1575), death);

    Create().ToYears(person, DisplayCalendar.Hebrew).Should().Equal(("1575", true), (" - ", false), ("† ", false), ("5400", false));
  }
}
