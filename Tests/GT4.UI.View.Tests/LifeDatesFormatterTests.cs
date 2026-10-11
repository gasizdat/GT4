using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
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

  private static readonly Date UnknownDate = Date.Create(0, DateStatus.Unknown);

  [Fact]
  public void ToYearsString_GivesBothYears()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Year(1820), Year(1885));

    Create().ToYearsString(person).Should().Be("1820 - † 1885");
  }

  [Fact]
  public void ToYearsString_TakesOnlyTheYearOfAWholeDate()
  {
    TestLanguage.Use(Language.EN);

    var birth = Date.Create(1820, 5, 14, DateStatus.WellKnown);
    var person = MakePerson(birth, null);

    Create().ToYearsString(person).Should().Be("1820");
  }

  // A segment has no room for "about 1840".
  [Fact]
  public void ToYearsString_MarksAnApproximateYear()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Year(1840, DateStatus.YearApproximate), null);

    Create().ToYearsString(person).Should().Be("~1840");
  }

  [Fact]
  public void ToYearsString_MarksAYearBeforeChrist()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Date.Create(-630000, DateStatus.MonthUnknown), Year(14));

    Create().ToYearsString(person).Should().Be("63 B.C. - † 14");
  }

  [Fact]
  public void ToYearsString_KeepsTheDeathMarkForAnUnknownDeathDate()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(Year(1820), UnknownDate);

    Create().ToYearsString(person).Should().Be("1820 - † ");
  }

  [Fact]
  public void ToYearsString_GivesOnlyTheDeathWhenTheBirthIsUnknown()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(UnknownDate, Year(1885));

    Create().ToYearsString(person).Should().Be("† 1885");
  }

  [Fact]
  public void ToYearsString_GivesNothingWhenNoYearIsKnown()
  {
    TestLanguage.Use(Language.EN);

    var person = MakePerson(UnknownDate, null);

    Create().ToYearsString(person).Should().BeEmpty();
  }
}
