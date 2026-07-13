using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI.Utils;

public record class ProjectStatistics(
  int TotalPersons,
  int TotalFamilies,
  int MenCount,
  int WomenCount,
  int UnknownSexCount,
  int LivingCount,
  double? AverageLifespanYears,
  double? Lifespan95thPercentileYears,
  PersonInfo? OldestLivingPerson,
  int? OldestLivingAgeYears,
  PersonInfo? LongestLifespanPerson,
  int? LongestLifespanYears,
  int? EarliestBirthYear,
  int? LatestBirthYear,
  int? MedianBirthYear,
  (int Decade, int Count)[] BirthsByDecade,
  (string Name, int Count)[] TopMaleFirstNames,
  (string Name, int Count)[] TopFemaleFirstNames,
  (string Name, int Count)[] TopLargestFamilies,
  string[] SingleMemberFamilyNames,
  int IncompleteBirthDateCount,
  int PhotoCoverageCount,
  int IsolatedPersonCount,
  int MarriageCount,
  double? AverageChildrenPerParent,
  PersonInfo? MostChildrenPerson,
  int MostChildrenCount
)
{
  public static ProjectStatistics Empty { get; } = ProjectStatisticsCalculator.Compute([], [], new Dictionary<int, Relative[]>());
}

/// <summary>
/// Pure aggregate-statistics computation over a project's persons/families/relatives, owned and driven
/// by <c>StatisticsPage</c> (GT4.UI.App). Mirrors the <see cref="PersonFilter"/>/<see cref="PersonLifetimeMatcher"/>
/// precedent: no DB access here, the page fetches the three inputs itself and passes them in.
/// </summary>
public static class ProjectStatisticsCalculator
{
  private const int TopNameCount = 5;
  private const int TopFamilyCount = 5;
  private const int SingleMemberFamilyPreviewCount = 5;

  private static bool HasKnownBirthYear(PersonInfo person) => person.BirthDate.Status != DateStatus.Unknown;

  private static bool HasKnownDeathYear(PersonInfo person) => person.DeathDate is { Status: not DateStatus.Unknown };

  private static double? Percentile(IReadOnlyList<int> sortedAscending, double fraction)
  {
    if (sortedAscending.Count == 0)
      return null;

    var index = Math.Clamp((int)Math.Ceiling(fraction * sortedAscending.Count) - 1, 0, sortedAscending.Count - 1);
    return sortedAscending[index];
  }

  private static int? Median(IReadOnlyList<int> sortedAscending)
  {
    if (sortedAscending.Count == 0)
      return null;

    var mid = sortedAscending.Count / 2;
    return sortedAscending.Count % 2 == 1
      ? sortedAscending[mid]
      : (int)Math.Round((sortedAscending[mid - 1] + sortedAscending[mid]) / 2.0);
  }

  private static (string Name, int Count)[] TopFirstNames(IEnumerable<PersonInfo> persons, BiologicalSex sex) =>
    persons
      .Where(p => p.BiologicalSex == sex)
      .SelectMany(p => p.Names.Where(n => n.Type.HasFlag(NameType.FirstName)))
      .GroupBy(n => n.Value)
      .Select(g => (Name: g.Key, Count: g.Count()))
      .OrderByDescending(x => x.Count)
      .Take(TopNameCount)
      .ToArray();

  public static ProjectStatistics Compute(
    PersonInfo[] persons, Name[] familyNames, IReadOnlyDictionary<int, Relative[]> relativesByPersonId)
  {
    var currentYear = Date.Now.Year;

    // Family-in-use: same person-by-family-name-id lookup ProjectPage.EnsureFamiliesLoaded already builds.
    var personsByFamilyNameId = persons
      .SelectMany(person => person.Names.Select(name => (NameId: name.Id, Person: person)))
      .ToLookup(x => x.NameId, x => x.Person);
    var familyMemberCounts = familyNames
      .Select(name => (Name: name, Count: personsByFamilyNameId[name.Id].Count()))
      .ToList();
    var usedFamilies = familyMemberCounts.Where(f => f.Count > 0).ToList();
    var topLargestFamilies = usedFamilies
      .OrderByDescending(f => f.Count)
      .Take(TopFamilyCount)
      .Select(f => (Name: f.Name.Value, f.Count))
      .ToArray();

    var livingPersons = persons.Where(p => PersonLifetimeMatcher.IsAliveInYear(p, currentYear)).ToList();

    var lifespans = persons
      .Where(p => HasKnownBirthYear(p) && HasKnownDeathYear(p))
      .Select(p => (Person: p, Years: (p.DeathDate!.Value - p.BirthDate).Years))
      .ToList();
    var sortedLifespans = lifespans.Select(x => x.Years).OrderBy(x => x).ToList();
    var longestLifespan = lifespans.OrderByDescending(x => x.Years).FirstOrDefault();

    var oldestLiving = livingPersons
      .Where(HasKnownBirthYear)
      .Select(p => (Person: p, Years: (Date.Now - p.BirthDate).Years))
      .OrderByDescending(x => x.Years)
      .FirstOrDefault();

    var knownBirthYears = persons.Where(HasKnownBirthYear).Select(p => p.BirthDate.Year).OrderBy(y => y).ToList();
    var birthsByDecade = knownBirthYears
      .GroupBy(y => y / 10 * 10)
      .Select(g => (Decade: g.Key, Count: g.Count()))
      .OrderBy(x => x.Decade)
      .ToArray();

    var incompleteBirthDateCount = persons.Count(p => p.BirthDate.Status is DateStatus.Unknown or DateStatus.YearApproximate);
    var photoCoverageCount = persons.Count(p => p.MainPhoto is not null);
    var isolatedPersonCount = persons.Count(p => !relativesByPersonId.TryGetValue(p.Id, out var rels) || rels.Length == 0);

    var spousePairs = new HashSet<(int, int)>();
    foreach (var person in persons)
    {
      if (!relativesByPersonId.TryGetValue(person.Id, out var relatives))
        continue;

      foreach (var spouse in relatives.Where(r => r.Type == RelationshipType.Spouse))
      {
        spousePairs.Add((Math.Min(person.Id, spouse.Id), Math.Max(person.Id, spouse.Id)));
      }
    }

    var childCounts = persons
      .Select(p => (Person: p, Count: relativesByPersonId.TryGetValue(p.Id, out var rels)
        ? rels.Count(r => r.Type is RelationshipType.Child or RelationshipType.AdoptiveChild)
        : 0))
      .Where(x => x.Count > 0)
      .ToList();
    var mostChildren = childCounts.OrderByDescending(x => x.Count).FirstOrDefault();

    return new ProjectStatistics(
      TotalPersons: persons.Length,
      TotalFamilies: usedFamilies.Count,
      MenCount: persons.Count(p => p.BiologicalSex == BiologicalSex.Male),
      WomenCount: persons.Count(p => p.BiologicalSex == BiologicalSex.Female),
      UnknownSexCount: persons.Count(p => p.BiologicalSex == BiologicalSex.Unknown),
      LivingCount: livingPersons.Count,
      AverageLifespanYears: lifespans.Count > 0 ? lifespans.Average(x => x.Years) : null,
      Lifespan95thPercentileYears: Percentile(sortedLifespans, 0.95),
      OldestLivingPerson: oldestLiving.Person,
      OldestLivingAgeYears: oldestLiving.Person is not null ? oldestLiving.Years : null,
      LongestLifespanPerson: longestLifespan.Person,
      LongestLifespanYears: longestLifespan.Person is not null ? longestLifespan.Years : null,
      EarliestBirthYear: knownBirthYears.Count > 0 ? knownBirthYears[0] : null,
      LatestBirthYear: knownBirthYears.Count > 0 ? knownBirthYears[^1] : null,
      MedianBirthYear: Median(knownBirthYears),
      BirthsByDecade: birthsByDecade,
      TopMaleFirstNames: TopFirstNames(persons, BiologicalSex.Male),
      TopFemaleFirstNames: TopFirstNames(persons, BiologicalSex.Female),
      TopLargestFamilies: topLargestFamilies,
      SingleMemberFamilyNames: [.. usedFamilies.Where(f => f.Count == 1).Select(f => f.Name.Value).Take(SingleMemberFamilyPreviewCount)],
      IncompleteBirthDateCount: incompleteBirthDateCount,
      PhotoCoverageCount: photoCoverageCount,
      IsolatedPersonCount: isolatedPersonCount,
      MarriageCount: spousePairs.Count,
      AverageChildrenPerParent: childCounts.Count > 0 ? childCounts.Average(x => x.Count) : null,
      MostChildrenPerson: mostChildren.Person,
      MostChildrenCount: mostChildren.Count
    );
  }
}
