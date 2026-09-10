using FluentAssertions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

public sealed class GedcomDateTests
{
  [Fact]
  public void WellKnownDate_RendersAndRoundTrips()
  {
    var date = Date.Create(20000115, DateStatus.WellKnown);

    var text = GedcomDate.ToGedcom(date);

    text.Should().Be("15 JAN 2000");
    GedcomDate.Parse(text).Should().Be(date);
  }

  [Fact]
  public void DayUnknown_OmitsDay()
  {
    var date = Date.Create(2000, 1, null, DateStatus.DayUnknown);

    var text = GedcomDate.ToGedcom(date);

    text.Should().Be("JAN 2000");
    GedcomDate.Parse(text).Should().Be(date);
  }

  [Fact]
  public void MonthUnknown_KeepsOnlyYear()
  {
    var date = Date.Create(2000, null, null, DateStatus.MonthUnknown);

    var text = GedcomDate.ToGedcom(date);

    text.Should().Be("2000");
    GedcomDate.Parse(text).Should().Be(date);
  }

  [Fact]
  public void YearApproximate_UsesAbtPrefix()
  {
    var date = Date.Create(1850, null, null, DateStatus.YearApproximate);

    var text = GedcomDate.ToGedcom(date);

    text.Should().Be("ABT 1850");
    GedcomDate.Parse(text).Should().Be(date);
  }

  [Fact]
  public void Unknown_ProducesNoText()
  {
    var date = new Date { Status = DateStatus.Unknown };

    GedcomDate.ToGedcom(date).Should().BeNull();
  }

  [Fact]
  public void BeforeChrist_RoundTrips()
  {
    var date = Date.Create(-10000101, DateStatus.WellKnown);

    var text = GedcomDate.ToGedcom(date);

    text.Should().Be("1 JAN 1000 B.C.");
    GedcomDate.Parse(text).Should().Be(date);
  }

  [Theory]
  [InlineData("BEF 1900")]
  [InlineData("AFT 1900")]
  [InlineData("BET 1900 AND 1910")]
  [InlineData("EST 1900")]
  [InlineData("CAL 1900")]
  public void RangeAndCalculatedQualifiers_CollapseToApproximateYear(string text)
  {
    var parsed = GedcomDate.Parse(text);

    parsed.Status.Should().Be(DateStatus.YearApproximate);
    parsed.Year.Should().Be(1900);
  }

  [Fact]
  public void Unparseable_ReturnsUnknown()
  {
    GedcomDate.Parse("not a date").Status.Should().Be(DateStatus.Unknown);
    GedcomDate.Parse(null).Status.Should().Be(DateStatus.Unknown);
  }

  [Fact]
  public void GregorianEscape_IsStrippedAndParsesNormally()
  {
    var parsed = GedcomDate.Parse("@#DGREGORIAN@ 4 JUL 1776");

    parsed.Should().Be(Date.Create(17760704, DateStatus.WellKnown));
  }

  [Theory]
  // Louis XVI's execution: 2 Pluviose An I -- the epoch, not a leap boundary.
  [InlineData("@#DFRENCH R@ 2 PLUV 1", 17930121)]
  // Marie Antoinette's execution: 25 Vendemiaire An II.
  [InlineData("@#DFRENCH R@ 25 VEND 2", 17931016)]
  // An III is the calendar's first sextile year; its epoch shifts An IV's start by a day (23 rather than
  // 22 September), the one place a fixed 30-day month count is not enough on its own.
  [InlineData("@#DFRENCH R@ 1 VEND 4", 17950923)]
  // Lowercase: GEDCOM data arrives from other people's exporters, not always uppercase.
  [InlineData("@#dfrench r@ 2 pluv 1", 17930121)]
  public void FrenchRepublicanEscape_ConvertsToGregorian(string text, int expectedCode)
  {
    var parsed = GedcomDate.Parse(text);

    parsed.Should().Be(Date.Create(expectedCode, DateStatus.WellKnown));
  }

  [Fact]
  public void FrenchRepublicanEscape_OutsideCalendarsOfficialUse_ReturnsUnknown()
  {
    GedcomDate.Parse("@#DFRENCH R@ 1 VEND 15").Status.Should().Be(DateStatus.Unknown);
  }

  [Theory]
  [InlineData("@#DFRENCH R@ 2 PLUV 1")]
  [InlineData("@#dfrench r@ 2 pluv 1")]
  public void FrenchRepublicanEscape_IsAConvertedCalendarEvenThoughItParses(string text)
  {
    GedcomDate.Parse(text).Status.Should().Be(DateStatus.WellKnown);
    GedcomDate.IsConvertedCalendar(text).Should().BeTrue();
  }
}
