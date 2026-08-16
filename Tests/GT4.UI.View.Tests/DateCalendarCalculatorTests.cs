using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class DateCalendarCalculatorTests
{
  private const int CurrentYear = 2026;

  private static Date WellKnown(int year, int month, int day) => Date.Create(year, month, day, DateStatus.WellKnown);

  private static Date DayUnknown(int year, int month) => Date.Create(year, month, null, DateStatus.DayUnknown);

  private static Date MonthUnknown(int year) => Date.Create(year, null, null, DateStatus.MonthUnknown);

  private static Date Approximate(int year) => Date.Create(year, null, null, DateStatus.YearApproximate);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo Person(int id, Date? birthDate = null, Date? deathDate = null) =>
    new(id, birthDate ?? UnknownDate, deathDate, BiologicalSex.Unknown, [], null);

  private static Relative Spouse(int id, Date? weddingDate = null) => new(Person(id), RelationshipType.Spouse, weddingDate);

  private static readonly Dictionary<int, Relative[]> NoRelatives = [];

  private static DateCalendarMonth Compute(
    PersonInfo[] persons, int month, Dictionary<int, Relative[]>? relativesByPersonId = null) =>
    DateCalendarCalculator.Compute(persons, relativesByPersonId ?? NoRelatives, month, CurrentYear);

  [Fact]
  public void Well_known_birth_date_is_placed_on_its_day_with_correct_years()
  {
    var person = Person(1, birthDate: WellKnown(1901, 8, 16));

    var month = Compute([person], 8);

    var day = Assert.Single(month.Days);
    Assert.Equal(16, day.Day);
    var entry = Assert.Single(day.Entries);
    Assert.Equal(DateCalendarEventType.Birth, entry.Type);
    Assert.Equal(125, entry.Years);
    Assert.True(entry.IsMilestone);
  }

  [Fact]
  public void Day_unknown_birth_date_lands_in_the_day_unknown_bucket_not_days()
  {
    var person = Person(1, birthDate: DayUnknown(1950, 8));

    var month = Compute([person], 8);

    Assert.Empty(month.Days);
    var entry = Assert.Single(month.DayUnknown);
    Assert.Equal(76, entry.Years);
  }

  [Theory]
  [MemberData(nameof(UnplaceableDates))]
  public void Dates_without_a_known_month_are_excluded_entirely(Date date)
  {
    var person = Person(1, birthDate: date);

    var month = Compute([person], 8);

    Assert.Empty(month.Days);
    Assert.Empty(month.DayUnknown);
  }

  public static TheoryData<Date> UnplaceableDates => new()
  {
    MonthUnknown(1950),
    Approximate(1950),
    UnknownDate,
  };

  [Fact]
  public void Death_date_follows_the_same_placement_rules_as_birth_date()
  {
    var person = Person(1, birthDate: UnknownDate, deathDate: WellKnown(1926, 8, 29));

    var month = Compute([person], 8);

    var day = Assert.Single(month.Days);
    var entry = Assert.Single(day.Entries);
    Assert.Equal(DateCalendarEventType.Death, entry.Type);
    Assert.Equal(100, entry.Years);
    Assert.True(entry.IsMilestone);
  }

  [Fact]
  public void No_death_date_recorded_produces_no_death_entry()
  {
    var person = Person(1, birthDate: WellKnown(1990, 8, 1), deathDate: null);

    var month = Compute([person], 8);

    var day = Assert.Single(month.Days);
    Assert.All(day.Entries, e => Assert.NotEqual(DateCalendarEventType.Death, e.Type));
  }

  [Fact]
  public void Symmetric_spouse_edges_collapse_to_a_single_wedding_entry()
  {
    var a = Person(1);
    var b = Person(2);
    var relatives = new Dictionary<int, Relative[]>
    {
      [a.Id] = [Spouse(b.Id, WellKnown(1976, 8, 22))],
      [b.Id] = [Spouse(a.Id, WellKnown(1976, 8, 22))],
    };

    var month = Compute([a, b], 8, relatives);

    var day = Assert.Single(month.Days);
    var entry = Assert.Single(day.Entries);
    Assert.Equal(DateCalendarEventType.Wedding, entry.Type);
    Assert.Equal(50, entry.Years);
  }

  [Fact]
  public void Conflicting_spouse_dates_resolve_first_seen()
  {
    var a = Person(1);
    var b = Person(2);
    // a's own iteration order comes first: a=>b's edge is seen before b=>a's.
    var relatives = new Dictionary<int, Relative[]>
    {
      [a.Id] = [Spouse(b.Id, WellKnown(1976, 8, 22))],
      [b.Id] = [Spouse(a.Id, WellKnown(1980, 1, 1))],
    };

    var month = Compute([a, b], 8, relatives);

    var day = Assert.Single(month.Days);
    Assert.Equal(22, day.Day);
  }

  [Theory]
  [InlineData(2000, false)] // 26 years
  [InlineData(2001, true)]  // 25 years
  [InlineData(2002, false)] // 24 years
  [InlineData(CurrentYear, false)] // 0 years
  public void Milestone_flags_only_multiples_of_25_and_never_year_zero(int birthYear, bool expectedMilestone)
  {
    var person = Person(1, birthDate: WellKnown(birthYear, 8, 16));

    var month = Compute([person], 8);

    var entry = Assert.Single(Assert.Single(month.Days).Entries);
    Assert.Equal(expectedMilestone, entry.IsMilestone);
  }

  [Fact]
  public void Entries_outside_the_requested_month_are_excluded()
  {
    var person = Person(1, birthDate: WellKnown(1950, 7, 16));

    var month = Compute([person], 8);

    Assert.Empty(month.Days);
    Assert.Empty(month.DayUnknown);
  }

  [Fact]
  public void Bc_birth_date_produces_the_correct_signed_year_age()
  {
    // Sign < 0 with Year=44 is 44 B.C.; astronomically that's year 1-44 = -43, so age in 2026 is 2026-(-43)=2069.
    var person = Person(1, birthDate: Date.Create(-440816, DateStatus.WellKnown));

    var month = Compute([person], 8);

    var entry = Assert.Single(Assert.Single(month.Days).Entries);
    Assert.Equal(2069, entry.Years);
  }

  [Fact]
  public void Feb_29_birth_renders_as_a_day_29_row_when_browsing_february()
  {
    var person = Person(1, birthDate: WellKnown(1904, 2, 29));

    var month = Compute([person], 2);

    var day = Assert.Single(month.Days);
    Assert.Equal(29, day.Day);
  }

  [Fact]
  public void CountUnplaceable_excludes_fully_unknown_and_missing_death_dates()
  {
    var noDataAtAll = Person(1, birthDate: UnknownDate, deathDate: null);
    var yearOnlyBirth = Person(2, birthDate: MonthUnknown(1900));
    var approximateBirth = Person(3, birthDate: Approximate(1900));
    var livingPerson = Person(4, birthDate: WellKnown(1990, 1, 1), deathDate: null);
    var yearOnlyDeath = Person(5, birthDate: WellKnown(1900, 1, 1), deathDate: MonthUnknown(1970));

    var count = DateCalendarCalculator.CountUnplaceable(
      [noDataAtAll, yearOnlyBirth, approximateBirth, livingPerson, yearOnlyDeath], NoRelatives);

    Assert.Equal(3, count);
  }
}
