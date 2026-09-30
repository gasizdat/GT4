using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Utils;

// Each statistic as StatisticsPage and the HTML export both show it, so the two read alike.
public sealed class ProjectStatisticsText(ProjectStatistics statistics, INameFormatter nameFormatter)
{
  // Kept past the end of the longest bar for its count to sit in, as a share of that bar.
  private const double CountGutterShare = 0.1;

  public string TotalPersons => statistics.TotalPersons.ToString();

  public string TotalFamilies => statistics.TotalFamilies.ToString();

  public string MenCount => statistics.MenCount.ToString();

  public string WomenCount => statistics.WomenCount.ToString();

  public string UnknownSexCount => statistics.UnknownSexCount.ToString();

  public string LivingCount => statistics.LivingCount.ToString();

  public string AverageLifespan => FormatYears(statistics.AverageLifespanYears);

  public string Lifespan95thPercentile => FormatYears(statistics.Lifespan95thPercentileYears);

  public string OldestLiving => FormatPersonYears(statistics.OldestLivingPerson, statistics.OldestLivingAgeYears);

  public string LongestLifespan => FormatPersonYears(statistics.LongestLifespanPerson, statistics.LongestLifespanYears);

  public string BirthYearSpan => statistics.EarliestBirthYear is not null && statistics.LatestBirthYear is not null
    ? string.Format(UIStrings.StatValueYearRange_2, statistics.EarliestBirthYear, statistics.LatestBirthYear)
    : UIStrings.StatValueNone;

  public string MedianBirthYear => statistics.MedianBirthYear?.ToString() ?? UIStrings.StatValueNone;

  public (string Decade, int Count)[] BirthsByDecade => [.. statistics.BirthsByDecade.Select(FormatDecade)];

  // A decade row's full length, in births: each bar is its own count long, so the rows stay comparable.
  public double DecadeRowLength
  {
    get
    {
      var counts = statistics.BirthsByDecade.Select(births => births.Count);
      var busiest = counts.DefaultIfEmpty().Max();
      return busiest * (1 + CountGutterShare);
    }
  }

  public string TopLargestFamilies => FormatNameCounts(statistics.TopLargestFamilies);

  public string SingleMemberFamilies => statistics.SingleMemberFamilyNames.Length > 0
    ? string.Join(", ", statistics.SingleMemberFamilyNames)
    : UIStrings.StatValueNone;

  public string TopMaleFirstNames => FormatNameCounts(statistics.TopMaleFirstNames);

  public string TopFemaleFirstNames => FormatNameCounts(statistics.TopFemaleFirstNames);

  public string IncompleteBirthDateCount => statistics.IncompleteBirthDateCount.ToString();

  public string PhotoCoverage => string.Format(UIStrings.StatValueCoverage_2, statistics.PhotoCoverageCount, statistics.TotalPersons);

  public string IsolatedPersonCount => statistics.IsolatedPersonCount.ToString();

  public string MarriageCount => statistics.MarriageCount.ToString();

  public string AverageChildren
  {
    get
    {
      if (statistics.AverageChildrenPerParent is not { } average)
        return UIStrings.StatValueNone;

      var value = average.ToString("F1");
      return string.Format(UIStrings.StatValueChildrenAverage_1, value);
    }
  }

  public string MostChildren
  {
    get
    {
      if (statistics.MostChildrenPerson is not { } person)
        return UIStrings.StatValueNone;

      var name = nameFormatter.ToString(person, NameFormat.CommonPersonName);
      return string.Format(UIStrings.StatValuePersonChildren_2, name, statistics.MostChildrenCount);
    }
  }

  private static string FormatYears(double? years)
  {
    if (years is not { } value)
      return UIStrings.StatValueNone;

    var formatted = value.ToString("F1");
    return string.Format(UIStrings.StatValueYears_1, formatted);
  }

  private string FormatPersonYears(PersonInfo? person, int? years)
  {
    if (person is null)
      return UIStrings.StatValueNone;

    var name = nameFormatter.ToString(person, NameFormat.CommonPersonName);
    return string.Format(UIStrings.StatValuePersonYears_2, name, years);
  }

  private static (string Decade, int Count) FormatDecade((int Decade, int Count) births)
  {
    var decade = string.Format(UIStrings.StatValueDecade_1, births.Decade);
    return (decade, births.Count);
  }

  private static string FormatNameCounts((string Name, int Count)[] items)
  {
    if (items.Length == 0)
      return UIStrings.StatValueNone;

    var counts = items.Select(item => string.Format(UIStrings.StatValueNameCount_2, item.Name, item.Count));
    return string.Join(", ", counts);
  }
}
