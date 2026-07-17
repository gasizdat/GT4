using FluentAssertions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Pure-logic coverage for <see cref="Date"/> and <see cref="DateSpan"/>: the packed-code
/// representation, status-driven truncation, comparison/equality operators and the date-difference
/// arithmetic. No database involved.
/// </summary>
public sealed class DateTests
{
  [Fact]
  public void Create_FromCode_WellKnown_DecodesEveryComponent()
  {
    var date = Date.Create(20230615, DateStatus.WellKnown);

    date.Sign.Should().Be(1);
    date.Year.Should().Be(2023);
    date.Month.Should().Be(6);
    date.Day.Should().Be(15);
    date.Status.Should().Be(DateStatus.WellKnown);
    date.Code.Should().Be(20230615);
  }

  [Fact]
  public void Create_FromNegativeCode_IsBeforeCommonEra()
  {
    var date = Date.Create(-1000101, DateStatus.WellKnown);

    date.Sign.Should().Be(-1);
    date.Year.Should().Be(100);
    date.Month.Should().Be(1);
    date.Day.Should().Be(1);
    date.Code.Should().Be(-1000101);
  }

  [Theory]
  [InlineData(DateStatus.DayUnknown, 2023, 6, 0)]
  [InlineData(DateStatus.MonthUnknown, 2023, 0, 0)]
  [InlineData(DateStatus.YearApproximate, 2023, 0, 0)]
  [InlineData(DateStatus.Unknown, 0, 0, 0)]
  public void Create_TruncatesComponents_AccordingToStatus(DateStatus status, int year, int month, int day)
  {
    var date = Date.Create(20230615, status);

    date.Year.Should().Be(year);
    date.Month.Should().Be(month);
    date.Day.Should().Be(day);
  }

  [Fact]
  public void Create_FromComponents_PacksCode()
  {
    var date = Date.Create(2023, 6, 15, DateStatus.WellKnown);

    date.Code.Should().Be(20230615);
  }

  [Fact]
  public void Create_FromComponents_WithNulls_TreatsThemAsZero()
  {
    var date = Date.Create(2023, null, null, DateStatus.DayUnknown);

    date.Year.Should().Be(2023);
    date.Month.Should().Be(0);
    date.Day.Should().Be(0);
  }

  [Fact]
  public void Create_FromComponents_NegativeYear_IsBeforeCommonEra()
  {
    var date = Date.Create(-2, 1, 1, DateStatus.WellKnown);

    date.Sign.Should().Be(-1);
    date.Year.Should().Be(2);
    date.Month.Should().Be(1);
    date.Day.Should().Be(1);
    date.Code.Should().Be(-20101);
  }

  [Fact]
  public void Create_FromDateTime_IsWellKnown()
  {
    var dt = new DateTime(1999, 12, 31);

    var date = Date.Create(dt);

    date.Sign.Should().Be(1);
    date.Year.Should().Be(1999);
    date.Month.Should().Be(12);
    date.Day.Should().Be(31);
    date.Status.Should().Be(DateStatus.WellKnown);
  }

  [Fact]
  public void Now_ReturnsTodaysComponents()
  {
    var now = Date.Now;
    var today = DateTime.Now;

    now.Year.Should().Be(today.Year);
    now.Month.Should().Be(today.Month);
    now.Status.Should().Be(DateStatus.WellKnown);
  }

  [Fact]
  public void Code_ZeroesComponentsBeyondStatus()
  {
    // A WellKnown construction read back at a coarser status zeroes the hidden components.
    var date = new Date { Sign = 1, Year = 2023, Month = 6, Day = 15, Status = DateStatus.MonthUnknown };

    date.Code.Should().Be(20230000);
  }

  [Fact]
  public void EqualityOperators_CompareCodeAndStatus()
  {
    var a = Date.Create(20230615, DateStatus.WellKnown);
    var b = Date.Create(20230615, DateStatus.WellKnown);
    var differentStatus = Date.Create(20230615, DateStatus.DayUnknown);

    (a == b).Should().BeTrue();
    (a != b).Should().BeFalse();
    (a == differentStatus).Should().BeFalse();
    (a != differentStatus).Should().BeTrue();
  }

  [Fact]
  public void Equals_And_GetHashCode_AreConsistent()
  {
    var a = Date.Create(20230615, DateStatus.WellKnown);
    var b = Date.Create(20230615, DateStatus.WellKnown);

    a.Equals((object)b).Should().BeTrue();
    a.Equals("not a date").Should().BeFalse();
    a.GetHashCode().Should().Be(b.GetHashCode());
  }

  [Fact]
  public void LessThan_OrdersByCode_WhenSignsMatch()
  {
    var earlier = Date.Create(20200101, DateStatus.WellKnown);
    var later = Date.Create(20230615, DateStatus.WellKnown);

    (earlier < later).Should().BeTrue();
    (later < earlier).Should().BeFalse();
  }

  [Fact]
  public void LessThan_NegativeSign_IsAlwaysBeforePositive()
  {
    var bce = Date.Create(-100, DateStatus.WellKnown);
    var ce = Date.Create(100, DateStatus.WellKnown);

    (bce < ce).Should().BeTrue();
    (ce < bce).Should().BeFalse();
  }

  [Fact]
  public void LessThan_BothBeforeCommonEra_LargerYearIsEarlier()
  {
    var earlier = Date.Create(-2000000, DateStatus.MonthUnknown); // 200 B.C.
    var later = Date.Create(-1000000, DateStatus.MonthUnknown); // 100 B.C.

    (earlier < later).Should().BeTrue();
    (later < earlier).Should().BeFalse();
  }

  [Fact]
  public void LessThan_SameYearBeforeCommonEra_MonthsRunForward()
  {
    var january = Date.Create(-500100, DateStatus.DayUnknown); // Jan 50 B.C.
    var march = Date.Create(-500300, DateStatus.DayUnknown); // Mar 50 B.C.

    (january < march).Should().BeTrue();
    (march < january).Should().BeFalse();
  }

  [Fact]
  public void GreaterThan_IsTrue_OnlyWhenStrictlyLater()
  {
    var earlier = Date.Create(20200101, DateStatus.WellKnown);
    var later = Date.Create(20230615, DateStatus.WellKnown);
    var sameAsLater = Date.Create(20230615, DateStatus.WellKnown);

    (later > earlier).Should().BeTrue();
    (earlier > later).Should().BeFalse();
    (later > sameAsLater).Should().BeFalse();
  }

  [Fact]
  public void Ordering_IsConsistent_ForEqualCodeDifferentStatus()
  {
    // Both truncate to the same packed code (the year only) but carry different certainty.
    var monthUnknown = Date.Create(20230000, DateStatus.MonthUnknown);
    var yearApprox = Date.Create(20230000, DateStatus.YearApproximate);

    monthUnknown.Code.Should().Be(yearApprox.Code);
    (monthUnknown == yearApprox).Should().BeFalse();

    // Exactly one direction is "greater" — never both and never neither.
    (monthUnknown > yearApprox).Should().NotBe(yearApprox > monthUnknown);
  }

  [Fact]
  public void GetWorstStatus_ReturnsTheLeastCertain()
  {
    var wellKnown = Date.Create(20230615, DateStatus.WellKnown);
    var dayUnknown = Date.Create(20230600, DateStatus.DayUnknown);

    Date.GetWorstStatus(wellKnown, dayUnknown).Should().Be(DateStatus.DayUnknown);
  }

  [Fact]
  public void Difference_WellKnown_ReturnsYearsMonthsDays()
  {
    var from = Date.Create(20200310, DateStatus.WellKnown);
    var to = Date.Create(20230615, DateStatus.WellKnown);

    var span = to - from;

    span.Years.Should().Be(3);
    span.Months.Should().Be(3);
    span.Days.Should().Be(5);
    span.Status.Should().Be(DateStatus.WellKnown);
  }

  [Fact]
  public void Difference_SwapsOperands_WhenToIsBeforeFrom()
  {
    var from = Date.Create(20200310, DateStatus.WellKnown);
    var to = Date.Create(20230615, DateStatus.WellKnown);

    // Reversed order must still yield a positive span.
    (from - to).Should().Be(to - from);
  }

  [Fact]
  public void Difference_WellKnown_BorrowsDaysAndMonths()
  {
    var from = Date.Create(20200320, DateStatus.WellKnown);
    var to = Date.Create(20230105, DateStatus.WellKnown);

    var span = to - from;

    // 5 - 20 days -> borrow a month (+30); 1 - 3 months -> borrow a year (+12).
    span.Days.Should().Be(15);
    span.Months.Should().Be(9);
    span.Years.Should().Be(2);
  }

  [Fact]
  public void Difference_DayUnknown_ReturnsYearsAndMonthsOnly()
  {
    var from = Date.Create(20200300, DateStatus.DayUnknown);
    var to = Date.Create(20230100, DateStatus.DayUnknown);

    var span = to - from;

    span.Days.Should().Be(0);
    span.Months.Should().Be(10); // 1 - 3 -> borrow a year (+12)
    span.Years.Should().Be(2);
    span.Status.Should().Be(DateStatus.DayUnknown);
  }

  [Theory]
  [InlineData(DateStatus.MonthUnknown)]
  [InlineData(DateStatus.YearApproximate)]
  public void Difference_MonthUnknownOrApproximate_ReturnsYearsOnly(DateStatus status)
  {
    var from = Date.Create(20200000, status);
    var to = Date.Create(20230000, status);

    var span = to - from;

    span.Years.Should().Be(3);
    span.Months.Should().Be(0);
    span.Days.Should().Be(0);
    span.Status.Should().Be(status);
  }

  [Fact]
  public void Difference_AcrossEraBoundary_HasNoYearZero()
  {
    var from = Date.Create(-20101, DateStatus.WellKnown); // 1 Jan 2 B.C.
    var to = Date.Create(20101, DateStatus.WellKnown); // 1 Jan 2 A.D.

    var span = to - from;

    // 2 B.C. -> 1 B.C. -> 1 A.D. -> 2 A.D.
    span.Years.Should().Be(3);
    span.Months.Should().Be(0);
    span.Days.Should().Be(0);
  }

  [Fact]
  public void Difference_BothBeforeCommonEra_ReturnsYearGap()
  {
    var from = Date.Create(-2000000, DateStatus.MonthUnknown); // 200 B.C.
    var to = Date.Create(-1000000, DateStatus.MonthUnknown); // 100 B.C.

    var span = to - from;

    span.Years.Should().Be(100);
  }

  [Fact]
  public void Difference_BothBeforeCommonEra_BorrowsMonths()
  {
    var from = Date.Create(-1000300, DateStatus.DayUnknown); // Mar 100 B.C.
    var to = Date.Create(-500100, DateStatus.DayUnknown); // Jan 50 B.C.

    var span = to - from;

    span.Years.Should().Be(49);
    span.Months.Should().Be(10);
  }

  [Fact]
  public void Difference_SwapsOperands_AcrossEraBoundary()
  {
    var from = Date.Create(-20101, DateStatus.WellKnown);
    var to = Date.Create(20101, DateStatus.WellKnown);

    (from - to).Should().Be(to - from);
  }

  [Fact]
  public void Difference_Unknown_ReturnsEmptySpan()
  {
    var from = Date.Create(0, DateStatus.Unknown);
    var to = Date.Create(20230101, DateStatus.WellKnown);

    var span = to - from;

    span.Years.Should().Be(0);
    span.Months.Should().Be(0);
    span.Days.Should().Be(0);
    span.Status.Should().Be(DateStatus.Unknown);
  }

  [Fact]
  public void DateSpan_RecordEquality_ComparesAllComponents()
  {
    var a = new DateSpan(1, 2, 3, DateStatus.WellKnown);
    var b = new DateSpan(1, 2, 3, DateStatus.WellKnown);
    var c = a with { Days = 4 };

    a.Should().Be(b);
    a.Should().NotBe(c);
  }
}
