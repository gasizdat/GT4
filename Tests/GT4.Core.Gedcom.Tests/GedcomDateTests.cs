using FluentAssertions;
using GT4.Core.Gedcom;
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
}
