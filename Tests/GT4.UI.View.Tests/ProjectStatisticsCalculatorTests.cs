using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class ProjectStatisticsCalculatorTests
{
  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static Date Known(int year) => Date.Create(year, 1, 1, DateStatus.WellKnown);

  private static Date Approximate(int year) => Date.Create(year, null, null, DateStatus.YearApproximate);

  private static Name FirstName(int nameId, string value) => new(nameId, value, NameType.FirstName, null);

  private static Name Family(int nameId, string value) => new(nameId, value, NameType.FamilyName, null);

  private static PersonInfo Person(
    int id,
    BiologicalSex sex = BiologicalSex.Unknown,
    Date? birthDate = null,
    Date? deathDate = null,
    Name[]? names = null,
    Data? mainPhoto = null) =>
    new(id, birthDate ?? UnknownDate, deathDate, sex, names ?? [], mainPhoto);

  private static Relative Spouse(int id) => new(Person(id), RelationshipType.Spouse, null);

  private static Relative Child(int id) => new(Person(id), RelationshipType.Child, null);

  private static readonly Dictionary<int, Relative[]> NoRelatives = [];

  private static ProjectStatistics Compute(
    PersonInfo[] persons, Name[]? familyNames = null, Dictionary<int, Relative[]>? relativesByPersonId = null) =>
    ProjectStatisticsCalculator.Compute(persons, familyNames ?? [], relativesByPersonId ?? NoRelatives);

  [Fact]
  public void Counts_total_persons_and_sex_breakdown_including_unknown()
  {
    var persons = new[]
    {
      Person(1, BiologicalSex.Male),
      Person(2, BiologicalSex.Male),
      Person(3, BiologicalSex.Female),
      Person(4, BiologicalSex.Unknown),
    };

    var stats = Compute(persons);

    Assert.Equal(4, stats.TotalPersons);
    Assert.Equal(2, stats.MenCount);
    Assert.Equal(1, stats.WomenCount);
    Assert.Equal(1, stats.UnknownSexCount);
  }

  [Fact]
  public void TotalFamilies_excludes_family_names_with_no_attached_persons()
  {
    var used = Family(100, "Used");
    var unused = Family(101, "Unused");
    var persons = new[] { Person(1, names: [used]) };

    var stats = Compute(persons, familyNames: [used, unused]);

    Assert.Equal(1, stats.TotalFamilies);
  }

  [Fact]
  public void LargestFamilyName_and_SingleMemberFamilyNames_are_derived_from_attached_person_counts()
  {
    var big = Family(100, "Big");
    var small = Family(101, "Small");
    var persons = new[]
    {
      Person(1, names: [big]),
      Person(2, names: [big]),
      Person(3, names: [small]),
    };

    var stats = Compute(persons, familyNames: [big, small]);

    Assert.Equal("Big", stats.LargestFamilyName);
    Assert.Equal(2, stats.LargestFamilySize);
    Assert.Equal(["Small"], stats.SingleMemberFamilyNames);
  }

  [Fact]
  public void LivingCount_uses_IsAliveInYear_not_a_naive_null_death_date_check()
  {
    var currentYear = Date.Now.Year;
    var stillAlive = Person(1, birthDate: Known(currentYear - 30));
    // No death date recorded but birth year is far beyond a plausible lifespan: not "living".
    var longDead = Person(2, birthDate: Known(currentYear - 200));

    var stats = Compute([stillAlive, longDead]);

    Assert.Equal(1, stats.LivingCount);
  }

  [Fact]
  public void Lifespan_stats_exclude_persons_with_unknown_birth_or_death_dates()
  {
    var known1 = Person(1, birthDate: Known(1900), deathDate: Known(1960)); // 60 years
    var known2 = Person(2, birthDate: Known(1900), deathDate: Known(1980)); // 80 years
    var unknownDeath = Person(3, birthDate: Known(1900), deathDate: null);
    var unknownBirth = Person(4, birthDate: UnknownDate, deathDate: Known(1950));

    var stats = Compute([known1, known2, unknownDeath, unknownBirth]);

    Assert.Equal(70, stats.AverageLifespanYears);
    Assert.Equal(80, stats.Lifespan95thPercentileYears);
    Assert.Equal(known2, stats.LongestLifespanPerson);
    Assert.Equal(80, stats.LongestLifespanYears);
  }

  [Fact]
  public void OldestLivingPerson_is_selected_among_living_persons_with_known_birth_year()
  {
    var currentYear = Date.Now.Year;
    var older = Person(1, birthDate: Known(currentYear - 80));
    var younger = Person(2, birthDate: Known(currentYear - 40));

    var stats = Compute([older, younger]);

    Assert.Equal(older, stats.OldestLivingPerson);
    Assert.Equal(80, stats.OldestLivingAgeYears);
  }

  [Fact]
  public void Birth_year_span_and_median_and_decades_use_only_known_birth_years()
  {
    var persons = new[]
    {
      Person(1, birthDate: Known(1900)),
      Person(2, birthDate: Known(1910)),
      Person(3, birthDate: Known(1950)),
      Person(4, birthDate: UnknownDate),
    };

    var stats = Compute(persons);

    Assert.Equal(1900, stats.EarliestBirthYear);
    Assert.Equal(1950, stats.LatestBirthYear);
    Assert.Equal(1910, stats.MedianBirthYear);
    Assert.Equal([(1900, 1), (1910, 1), (1950, 1)], stats.BirthsByDecade);
  }

  [Fact]
  public void IncompleteBirthDateCount_counts_unknown_and_year_approximate_but_not_well_known()
  {
    var persons = new[]
    {
      Person(1, birthDate: UnknownDate),
      Person(2, birthDate: Approximate(1900)),
      Person(3, birthDate: Known(1900)),
    };

    var stats = Compute(persons);

    Assert.Equal(2, stats.IncompleteBirthDateCount);
  }

  [Fact]
  public void TopFirstNames_are_grouped_per_sex_and_capped_at_five_with_fewer_available()
  {
    var persons = new[]
    {
      Person(1, BiologicalSex.Male, names: [FirstName(1, "John")]),
      Person(2, BiologicalSex.Male, names: [FirstName(2, "John")]),
      Person(3, BiologicalSex.Male, names: [FirstName(3, "Mark")]),
      Person(4, BiologicalSex.Female, names: [FirstName(4, "Jane")]),
    };

    var stats = Compute(persons);

    Assert.Equal([("John", 2), ("Mark", 1)], stats.TopMaleFirstNames);
    Assert.Equal([("Jane", 1)], stats.TopFemaleFirstNames);
  }

  [Fact]
  public void PhotoCoverageCount_counts_persons_with_a_main_photo()
  {
    var withPhoto = Person(1, mainPhoto: new Data(1, [], null, DataCategory.PersonMainPhoto));
    var withoutPhoto = Person(2);

    var stats = Compute([withPhoto, withoutPhoto]);

    Assert.Equal(1, stats.PhotoCoverageCount);
  }

  [Fact]
  public void IsolatedPersonCount_counts_persons_with_no_relatives_entry_or_an_empty_one()
  {
    var missingEntry = Person(1);
    var emptyEntry = Person(2);
    var hasRelative = Person(3);
    var relatives = new Dictionary<int, Relative[]>
    {
      [emptyEntry.Id] = [],
      [hasRelative.Id] = [Spouse(4)],
    };

    var stats = Compute([missingEntry, emptyEntry, hasRelative], relativesByPersonId: relatives);

    Assert.Equal(2, stats.IsolatedPersonCount);
  }

  [Fact]
  public void MarriageCount_does_not_double_count_a_symmetric_spouse_pair()
  {
    var a = Person(1);
    var b = Person(2);
    var relatives = new Dictionary<int, Relative[]>
    {
      [a.Id] = [Spouse(b.Id)],
      [b.Id] = [Spouse(a.Id)],
    };

    var stats = Compute([a, b], relativesByPersonId: relatives);

    Assert.Equal(1, stats.MarriageCount);
  }

  [Fact]
  public void Children_stats_average_and_max_exclude_persons_with_no_recorded_children()
  {
    var childless = Person(1);
    var parentOfTwo = Person(2);
    var parentOfFour = Person(3);
    var relatives = new Dictionary<int, Relative[]>
    {
      [parentOfTwo.Id] = [Child(10), Child(11)],
      [parentOfFour.Id] = [Child(20), Child(21), Child(22), Child(23)],
    };

    var stats = Compute([childless, parentOfTwo, parentOfFour], relativesByPersonId: relatives);

    Assert.Equal(3, stats.AverageChildrenPerParent);
    Assert.Equal(parentOfFour, stats.MostChildrenPerson);
    Assert.Equal(4, stats.MostChildrenCount);
  }
}
