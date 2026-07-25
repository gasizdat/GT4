using GT4.Core.Gedcom;

namespace GT4.Tools.RelativesCli;

internal enum GedcomDifferenceKind
{
  UnmatchedIndividual,
  MissingEdge,
  ExtraEdge,
  MarriageDate,
}

internal readonly record struct GedcomDifference(GedcomDifferenceKind Kind, string Subject, string Detail);

/// <summary>
/// Compares two GEDCOM files by what their tags say rather than by their text. Export renumbers every xref
/// and regenerates the FAM records from GT4's edge graph, so nothing that identifies a record survives:
/// individuals are matched on content (and, where that is ambiguous, on the company they keep) and the
/// relationships are then compared in that matched space, never as FAM records.
/// </summary>
internal static class GedcomComparer
{
  public static async Task<GedcomDifference[]> CompareAsync(TextReader left, TextReader right, CancellationToken token)
  {
    var leftRoots = await GedcomReader.ReadAsync(left, token);
    var rightRoots = await GedcomReader.ReadAsync(right, token);
    var leftSide = Side.Build(leftRoots);
    var rightSide = Side.Build(rightRoots);

    var differences = new List<GedcomDifference>();
    MatchIndividuals(leftSide, rightSide, differences);
    CompareEdges(leftSide, rightSide, differences);
    return [.. differences];
  }

  /// <summary>
  /// Pairs individuals across the two sides. Content-unique people match outright; the rest are
  /// disambiguated by the identities of their already-matched relatives, repeatedly, until that stops
  /// resolving anything. Whoever is left over on either side is reported, so the edge comparison below can
  /// trust that a missing edge means a missing edge and not an unrecognized person.
  /// </summary>
  private static void MatchIndividuals(Side left, Side right, List<GedcomDifference> differences)
  {
    var buckets = BuildBuckets(left, right, individual => individual.Key);
    var nextMatchId = 0;
    foreach (var (leftGroup, rightGroup) in buckets)
    {
      if (leftGroup.Count == 1 && rightGroup.Count == 1)
      {
        Pair(leftGroup[0], rightGroup[0], nextMatchId++);
      }
    }

    // Refine what is still ambiguous: a nameless spouse is identified by whom they married, which needs
    // that spouse matched first -- so this converges over several passes rather than one.
    bool resolved;
    do
    {
      resolved = false;
      foreach (var (leftGroup, rightGroup) in buckets)
      {
        var leftPending = leftGroup.Where(Unmatched).ToArray();
        var rightPending = rightGroup.Where(Unmatched).ToArray();
        if (leftPending.Length == 0 || rightPending.Length == 0)
          continue;

        var leftBySignature = leftPending.ToLookup(individual => Signature(individual, left));
        var rightBySignature = rightPending.ToLookup(individual => Signature(individual, right));
        foreach (var signatureGroup in leftBySignature)
        {
          var counterparts = rightBySignature[signatureGroup.Key].ToArray();
          if (signatureGroup.Count() != 1 || counterparts.Length != 1)
            continue;

          Pair(signatureGroup.Single(), counterparts[0], nextMatchId++);
          resolved = true;
        }
      }
    }
    while (resolved);

    // Same content, same relatives, still several of them: they are interchangeable, so pair them off in
    // document order and report only the surplus.
    foreach (var (leftGroup, rightGroup) in buckets)
    {
      var leftPending = leftGroup.Where(Unmatched).ToArray();
      var rightPending = rightGroup.Where(Unmatched).ToArray();
      var shared = Math.Min(leftPending.Length, rightPending.Length);
      for (var i = 0; i < shared; i++)
      {
        Pair(leftPending[i], rightPending[i], nextMatchId++);
      }
      foreach (var surplus in leftPending.Skip(shared))
      {
        differences.Add(new GedcomDifference(GedcomDifferenceKind.UnmatchedIndividual, surplus.Label, "only in the first file"));
      }
      foreach (var surplus in rightPending.Skip(shared))
      {
        differences.Add(new GedcomDifference(GedcomDifferenceKind.UnmatchedIndividual, surplus.Label, "only in the second file"));
      }
    }
  }

  private static void CompareEdges(Side left, Side right, List<GedcomDifference> differences)
  {
    var leftSpouses = SpouseEdges(left);
    var rightSpouses = SpouseEdges(right);
    ReportEdgeDifferences(leftSpouses.Keys, rightSpouses.Keys, "spouse", differences);

    foreach (var (couple, leftDates) in leftSpouses)
    {
      if (!rightSpouses.TryGetValue(couple, out var rightDates) || leftDates.SequenceEqual(rightDates))
        continue;

      var expected = string.Join(", ", leftDates);
      var actual = string.Join(", ", rightDates);
      differences.Add(new GedcomDifference(GedcomDifferenceKind.MarriageDate, couple.Label, $"[{expected}] vs [{actual}]"));
    }

    var leftChildren = ChildEdges(left);
    var rightChildren = ChildEdges(right);
    ReportEdgeDifferences(leftChildren, rightChildren, "parent-child", differences);
  }

  private static void ReportEdgeDifferences(IEnumerable<Edge> left, IEnumerable<Edge> right, string kind, List<GedcomDifference> differences)
  {
    var missing = left.Except(right);
    foreach (var edge in missing)
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.MissingEdge, edge.Label, $"{kind} edge only in the first file"));
    }

    var extra = right.Except(left);
    foreach (var edge in extra)
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.ExtraEdge, edge.Label, $"{kind} edge only in the second file"));
    }
  }

  /// <summary>
  /// Couples, keyed by matched identity, with the marriage dates recorded for them. A couple split across
  /// several FAM records (the source's shape) and one merged FAM (what GT4 regenerates) must come out the
  /// same, so the dates of every FAM naming the pair are pooled.
  /// </summary>
  private static Dictionary<Edge, string[]> SpouseEdges(Side side)
  {
    var edges = new Dictionary<Edge, List<string>>();
    foreach (var family in side.Families)
    {
      var husband = side.Resolve(family.Husband);
      var wife = side.Resolve(family.Wife);
      if (husband is null || wife is null || Unmatched(husband) || Unmatched(wife))
        continue;

      var edge = Edge.Between(husband, wife);
      if (!edges.TryGetValue(edge, out var dates))
      {
        dates = [];
        edges[edge] = dates;
      }
      dates.AddRange(family.MarriageDates);
    }

    return edges.ToDictionary(entry => entry.Key, entry => entry.Value.Order().ToArray());
  }

  private static Edge[] ChildEdges(Side side)
  {
    var edges = new List<Edge>();
    foreach (var family in side.Families)
    {
      var parents = new[] { side.Resolve(family.Husband), side.Resolve(family.Wife) };
      foreach (var childXref in family.Children)
      {
        var child = side.Resolve(childXref);
        if (child is null || Unmatched(child))
          continue;

        var adopted = side.IsAdopted(child, family.Xref);
        foreach (var parent in parents)
        {
          if (parent is null || Unmatched(parent))
            continue;

          edges.Add(Edge.From(parent, child, adopted));
        }
      }
    }

    return [.. edges.Distinct()];
  }

  /// <summary>
  /// What an individual looks like from the outside: their own content plus the identity of every relative
  /// already matched. Unmatched relatives contribute nothing, so a person is never distinguished by a
  /// neighbour the other side simply failed to resolve.
  /// </summary>
  private static string Signature(Individual individual, Side side)
  {
    var neighbours = individual.Neighbours
      .Select(neighbour => (Role: neighbour.Role, Person: side.Resolve(neighbour.Xref)))
      .Where(neighbour => neighbour.Person is not null && !Unmatched(neighbour.Person))
      .Select(neighbour => $"{neighbour.Role}{neighbour.Person!.MatchId}")
      .Order();
    var joined = string.Join(",", neighbours);
    return $"{individual.Key}|{joined}";
  }

  private static List<(List<Individual> Left, List<Individual> Right)> BuildBuckets(Side left, Side right, Func<Individual, string> key)
  {
    var buckets = new Dictionary<string, (List<Individual> Left, List<Individual> Right)>();
    foreach (var individual in left.Individuals)
    {
      var bucket = GetBucket(buckets, key(individual));
      bucket.Left.Add(individual);
    }
    foreach (var individual in right.Individuals)
    {
      var bucket = GetBucket(buckets, key(individual));
      bucket.Right.Add(individual);
    }
    return [.. buckets.Values];
  }

  private static (List<Individual> Left, List<Individual> Right) GetBucket(
    Dictionary<string, (List<Individual> Left, List<Individual> Right)> buckets,
    string key)
  {
    if (buckets.TryGetValue(key, out var existing))
      return existing;

    var created = (Left: new List<Individual>(), Right: new List<Individual>());
    buckets[key] = created;
    return created;
  }

  private static void Pair(Individual left, Individual right, int matchId)
  {
    left.MatchId = matchId;
    right.MatchId = matchId;
  }

  private static bool Unmatched(Individual individual) => individual.MatchId < 0;

  /// <summary>A relationship expressed in matched-identity space, so it is comparable across the two files.</summary>
  private readonly record struct Edge(string Key, string Label)
  {
    public static Edge Between(Individual first, Individual second)
    {
      var ordered = first.MatchId <= second.MatchId ? (First: first, Second: second) : (First: second, Second: first);
      var key = $"{ordered.First.MatchId}+{ordered.Second.MatchId}";
      return new Edge(key, $"{ordered.First.Label} + {ordered.Second.Label}");
    }

    public static Edge From(Individual parent, Individual child, bool adopted)
    {
      var prefix = adopted ? "adopt:" : string.Empty;
      var key = $"{prefix}{parent.MatchId}->{child.MatchId}";
      return new Edge(key, $"{prefix}{parent.Label} -> {child.Label}");
    }

    public bool Equals(Edge other) => Key == other.Key;

    public override int GetHashCode() => Key.GetHashCode();
  }

  private sealed class Individual
  {
    public required string Xref { get; init; }
    public required GedcomNode Node { get; init; }
    public required string Key { get; init; }
    public required string Label { get; init; }
    public List<(string Role, string Xref)> Neighbours { get; } = [];
    public int MatchId { get; set; } = -1;
  }

  private sealed record Family(string Xref, string? Husband, string? Wife, string[] Children, string[] MarriageDates);

  private sealed class Side
  {
    public required Individual[] Individuals { get; init; }
    public required Family[] Families { get; init; }
    public required Dictionary<string, Individual> ByXref { get; init; }

    public static Side Build(GedcomNode[] roots)
    {
      var individuals = roots
        .Where(root => root.Tag == GedcomTags.Individual && root.Xref is not null)
        .Select(Describe)
        .ToArray();
      var byXref = individuals.ToDictionary(individual => individual.Xref);
      var families = roots.Where(root => root.Tag == GedcomTags.Family).Select(ReadFamily).ToArray();

      var side = new Side { Individuals = individuals, Families = families, ByXref = byXref };
      side.LinkNeighbours();
      return side;
    }

    public Individual? Resolve(string? xref)
    {
      if (xref is null)
        return null;

      ByXref.TryGetValue(xref, out var individual);
      return individual;
    }

    public bool IsAdopted(Individual child, string familyXref)
    {
      var links = child.Node.ChildrenWithTag(GedcomTags.FamilyChild);
      var link = links.FirstOrDefault(node => node.Value == familyXref);
      return link?.ChildValue(GedcomTags.Pedigree) == GedcomTags.AdoptedPedigree;
    }

    private void LinkNeighbours()
    {
      foreach (var family in Families)
      {
        var husband = Resolve(family.Husband);
        var wife = Resolve(family.Wife);
        husband?.Neighbours.Add(("S", family.Wife!));
        wife?.Neighbours.Add(("S", family.Husband!));
        foreach (var childXref in family.Children)
        {
          var child = Resolve(childXref);
          foreach (var parent in new[] { husband, wife })
          {
            if (parent is null)
              continue;

            parent.Neighbours.Add(("C", childXref));
            child?.Neighbours.Add(("P", parent.Xref));
          }
        }
      }
    }

    private static Individual Describe(GedcomNode node)
    {
      var nameNode = node.Child(GedcomTags.Name);
      var parts = nameNode is null ? (Given: null, Surname: null) : GedcomName.Parts(nameNode);
      var given = Collapse(parts.Given);
      var surname = Collapse(parts.Surname);
      var sex = node.ChildValue(GedcomTags.Sex) ?? string.Empty;
      var birth = EventDate(node, GedcomTags.Birth);
      var death = EventDate(node, GedcomTags.Death);
      return new Individual
      {
        Xref = node.Xref!,
        Node = node,
        Key = $"{given}|{surname}|{sex}|{birth}|{death}",
        Label = $"{given} /{surname}/ [{sex}] b.{birth} d.{death}",
      };
    }

    private static Family ReadFamily(GedcomNode node)
    {
      var children = node
        .ChildrenWithTag(GedcomTags.Child)
        .Select(child => child.Value)
        .Where(value => value is not null)
        .ToArray();
      var marriages = node
        .ChildrenWithTag(GedcomTags.Marriage)
        .Select(marriage => marriage.ChildValue(GedcomTags.Date))
        .Select(CanonicalDate)
        .ToArray();
      return new Family(
        node.Xref ?? string.Empty,
        node.ChildValue(GedcomTags.Husband),
        node.ChildValue(GedcomTags.Wife),
        children!,
        marriages);
    }

    private static string EventDate(GedcomNode individual, string tag)
    {
      var eventNode = individual.Child(tag);
      var raw = eventNode?.ChildValue(GedcomTags.Date);
      return CanonicalDate(raw);
    }

    /// <summary>
    /// A date as GT4 is able to hold it: the same collapse import applies, so a qualifier GT4 cannot
    /// express ("BET x AND y") compares equal to the approximate year it becomes. A date GT4 cannot parse
    /// at all yields nothing on both sides -- that loss is reported separately, not through matching.
    /// </summary>
    private static string CanonicalDate(string? value)
    {
      var parsed = GedcomDate.Parse(value);
      var rendered = GedcomDate.ToGedcom(parsed);
      return rendered ?? string.Empty;
    }

    private static string Collapse(string? value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

      var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
      return string.Join(' ', tokens);
    }
  }
}
