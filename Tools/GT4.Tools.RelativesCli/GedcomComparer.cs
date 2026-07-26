using GT4.Core.Gedcom;

namespace GT4.Tools.RelativesCli;

internal enum GedcomDifferenceKind
{
  UnmatchedIndividual,
  MissingEdge,
  ExtraEdge,
  MarriageDate,
  Tag,
  Media,
  NotRepresentable,
  Record,
}

internal readonly record struct GedcomDifference(GedcomDifferenceKind Kind, string Subject, string Detail);

/// <summary>
/// Compares two GEDCOM files by what their tags say rather than by their text. Export renumbers every xref
/// and regenerates the FAM records from GT4's edge graph, so nothing that identifies a record survives:
/// individuals are matched on content (and, where that is ambiguous, on the company they keep) and the
/// relationships are then compared in that matched space, never as FAM records.
///
/// Anything else export derives rather than copies is likewise compared as what it means, not as what it
/// says: a NAME value rebuilt from GIVN/SURN, a NOTE pointer inlined into a biography, a date collapsed
/// through <see cref="GedcomDate"/>, a resolved photo re-encoded from a FILE reference to a BLOB.
/// </summary>
internal static class GedcomComparer
{
  // Carried by the edge and media comparisons rather than as text: the family pointers say nothing the
  // edges do not, and OBJE is re-encoded on export so it never compares literally.
  private static readonly HashSet<string> StructuralTags =
    [GedcomTags.FamilySpouse, GedcomTags.FamilyChild, GedcomTags.Object];

  public static async Task<GedcomDifference[]> CompareAsync(TextReader left, TextReader right, CancellationToken token)
  {
    var leftRoots = await GedcomReader.ReadAsync(left, token);
    var rightRoots = await GedcomReader.ReadAsync(right, token);
    var leftSide = Side.Build(leftRoots);
    var rightSide = Side.Build(rightRoots);

    var differences = new List<GedcomDifference>();
    MatchIndividuals(leftSide, rightSide, differences);
    CompareEdges(leftSide, rightSide, differences);
    CompareContent(leftSide, rightSide, differences);
    ComparePassthroughRecords(leftSide, rightSide, differences);
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
    var buckets = BuildBuckets(left, right);
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
  /// Every unmodeled tag GT4 claims to keep as residue (PLAC, SOUR, OCCU, CHAN, ...) has to survive
  /// verbatim, which is what this checks. FAMS/FAMC go to the edge comparison and OBJE to
  /// <see cref="CompareMedia"/> instead.
  /// </summary>
  private static void CompareContent(Side left, Side right, List<GedcomDifference> differences)
  {
    var counterparts = right.Individuals.Where(individual => !Unmatched(individual)).ToDictionary(individual => individual.MatchId);
    foreach (var individual in left.Individuals)
    {
      if (Unmatched(individual))
        continue;

      var counterpart = counterparts[individual.MatchId];
      ReportUnrepresentableDates(individual, counterpart, differences);
      CompareTags(individual, counterpart, left, right, differences);
      CompareMedia(individual, counterpart, differences);
    }
  }

  private static void CompareTags(Individual left, Individual right, Side leftSide, Side rightSide, List<GedcomDifference> differences)
  {
    var leftTags = leftSide.ContentTags(left);
    var rightTags = rightSide.ContentTags(right);
    foreach (var text in Surplus(leftTags, rightTags))
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.Tag, left.Label, $"only in the first file: {text}"));
    }
    foreach (var text in Surplus(rightTags, leftTags))
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.Tag, left.Label, $"only in the second file: {text}"));
    }
  }

  /// <summary>
  /// Media is identified by its TITL, since export re-encodes a resolved external FILE reference as an
  /// embedded BLOB and stops emitting the filename. Media nested under an event, or on a couple's FAM,
  /// lands on the person, so it counts towards the person's set on both sides.
  /// </summary>
  private static void CompareMedia(Individual left, Individual right, List<GedcomDifference> differences)
  {
    foreach (var title in Surplus(left.MediaTitles, right.MediaTitles))
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.Media, left.Label, $"media \"{title}\" only in the first file"));
    }
    foreach (var title in Surplus(right.MediaTitles, left.MediaTitles))
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.Media, left.Label, $"media \"{title}\" only in the second file"));
    }
  }

  /// <summary>
  /// A date the source states but <see cref="GedcomDate"/> cannot parse -- a "@#DFRENCH R@ ..." calendar
  /// escape, see issue #173 -- becomes nothing on import, and would otherwise compare equal to the nothing
  /// on the other side, certifying the loss as fidelity.
  /// </summary>
  private static void ReportUnrepresentableDates(Individual left, Individual right, List<GedcomDifference> differences)
  {
    foreach (var tag in new[] { GedcomTags.Birth, GedcomTags.Death })
    {
      var raw = EventDateValue(left.Node, tag);
      if (string.IsNullOrWhiteSpace(raw))
        continue;

      var canonical = CanonicalDate(raw);
      var counterpart = EventDateValue(right.Node, tag);
      if (canonical.Length > 0 || raw == counterpart)
        continue;

      differences.Add(new GedcomDifference(GedcomDifferenceKind.NotRepresentable, left.Label, $"{tag} date \"{raw}\" survives as \"{counterpart}\""));
    }
  }

  /// <summary>
  /// The top-level records GT4 has no schema for but stores verbatim (submitter, submission, source,
  /// repository, multimedia). The ones it drops outright -- HEAD, NOTE -- are not compared; see
  /// unsupported-tags.md.
  /// </summary>
  private static void ComparePassthroughRecords(Side left, Side right, List<GedcomDifference> differences)
  {
    var leftRecords = left.PassthroughRecords();
    var rightRecords = right.PassthroughRecords();
    foreach (var record in Surplus(leftRecords, rightRecords))
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.Record, record.Label, $"only in the first file: {record.Text}"));
    }
    foreach (var record in Surplus(rightRecords, leftRecords))
    {
      differences.Add(new GedcomDifference(GedcomDifferenceKind.Record, record.Label, $"only in the second file: {record.Text}"));
    }
  }

  /// <summary>
  /// A node as a canonical string. Children are sorted, so re-emission order is not a difference, and an
  /// empty node is nothing at all -- a bare "1 BIRT" says nothing and does not survive import.
  /// </summary>
  private static string Serialize(GedcomNode node, IReadOnlyDictionary<string, string> notes)
  {
    var parts = new List<string>();
    foreach (var child in node.Children)
    {
      if (child.Tag == GedcomTags.Object || IsDerivedNamePart(node, child))
        continue;

      // A modeled date GT4 cannot parse becomes nothing on export, and ReportUnrepresentableDates already
      // says so -- serializing the empty result would report the same loss a second time here.
      var text = IsModeledDate(node, child) ? ModeledDate(child) : Serialize(child, notes);
      if (text.Length > 0)
      {
        parts.Add(text);
      }
    }
    parts.Sort(StringComparer.Ordinal);

    var value = NodeValue(node, notes);
    if (value.Length == 0 && parts.Count == 0)
      return string.Empty;

    var joined = string.Join("; ", parts);
    return $"{node.Tag}={value}[{joined}]";
  }

  private static bool IsDerivedNamePart(GedcomNode parent, GedcomNode child) =>
    parent.Tag == GedcomTags.Name && child.Tag is GedcomTags.Given or GedcomTags.Surname;

  private static bool IsModeledDate(GedcomNode parent, GedcomNode child) =>
    child.Tag == GedcomTags.Date && parent.Tag is GedcomTags.Birth or GedcomTags.Death;

  private static string ModeledDate(GedcomNode date)
  {
    var canonical = CanonicalDate(date.Value);
    return canonical.Length == 0 ? string.Empty : $"{GedcomTags.Date}={canonical}";
  }

  private static string NodeValue(GedcomNode node, IReadOnlyDictionary<string, string> notes)
  {
    if (node.Tag == GedcomTags.Note)
      return ResolveNote(node.Value, notes);

    if (node.Tag != GedcomTags.Name)
      return (node.Value ?? string.Empty).Trim();

    var (given, surname) = GedcomName.Parts(node);
    var collapsedGiven = Collapse(given);
    var collapsedSurname = Collapse(surname);
    return collapsedGiven.Length == 0 && collapsedSurname.Length == 0 ? string.Empty : $"{collapsedGiven}|{collapsedSurname}";
  }

  /// <summary>
  /// A NOTE carried as a pointer to a top-level record is the same note as one written inline -- import
  /// pulls the record's text into the person's biography and export writes it out inline, so the pointer
  /// never survives as such.
  /// </summary>
  private static string ResolveNote(string? value, IReadOnlyDictionary<string, string> notes)
  {
    var trimmed = (value ?? string.Empty).Trim();
    if (notes.TryGetValue(trimmed, out var text))
      return text.Trim();

    return trimmed;
  }

  private static string? EventDateValue(GedcomNode individual, string tag)
  {
    var eventNode = individual.Child(tag);
    return eventNode?.ChildValue(GedcomTags.Date);
  }

  /// <summary>What <paramref name="from"/> holds that <paramref name="against"/> does not, counting duplicates.</summary>
  private static IEnumerable<T> Surplus<T>(IEnumerable<T> from, IEnumerable<T> against) where T : notnull
  {
    var available = new Dictionary<T, int>();
    foreach (var item in against)
    {
      available[item] = available.GetValueOrDefault(item) + 1;
    }

    foreach (var item in from)
    {
      var remaining = available.GetValueOrDefault(item);
      if (remaining > 0)
      {
        available[item] = remaining - 1;
        continue;
      }
      yield return item;
    }
  }

  /// <summary>
  /// A date as GT4 is able to hold it: the same collapse import applies, so a qualifier GT4 cannot express
  /// ("BET x AND y") compares equal to the approximate year it becomes.
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

        var adopted = Side.IsAdopted(child, family.Xref);
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

  /// <summary>Individuals of both sides grouped by content key, so each group holds the candidates to pair.</summary>
  private static List<(List<Individual> Left, List<Individual> Right)> BuildBuckets(Side left, Side right)
  {
    var buckets = new Dictionary<string, (List<Individual> Left, List<Individual> Right)>();

    (List<Individual> Left, List<Individual> Right) Bucket(string key)
    {
      if (!buckets.TryGetValue(key, out var bucket))
      {
        bucket = (Left: [], Right: []);
        buckets[key] = bucket;
      }
      return bucket;
    }

    foreach (var individual in left.Individuals)
    {
      var bucket = Bucket(individual.Key);
      bucket.Left.Add(individual);
    }
    foreach (var individual in right.Individuals)
    {
      var bucket = Bucket(individual.Key);
      bucket.Right.Add(individual);
    }
    return [.. buckets.Values];
  }

  private static void Pair(Individual left, Individual right, int matchId)
  {
    left.MatchId = matchId;
    right.MatchId = matchId;
  }

  private static bool Unmatched(Individual individual) => individual.MatchId < 0;

  private readonly record struct Passthrough(string Label, string Text);

  /// <summary>
  /// A relationship expressed in matched-identity space, so it is comparable across the two files.
  /// <see cref="Label"/> is for display only and stays out of equality.
  /// </summary>
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
    public List<string> MediaTitles { get; } = [];
    public int MatchId { get; set; } = -1;
  }

  private sealed record Family(string Xref, string? Husband, string? Wife, string[] Children, string[] MarriageDates);

  private sealed class Side
  {
    private readonly GedcomNode[] _roots;
    private readonly Dictionary<string, Individual> _byXref;
    private readonly Dictionary<string, string> _notes;

    private Side(GedcomNode[] roots, Individual[] individuals, Family[] families, Dictionary<string, string> notes)
    {
      _roots = roots;
      _byXref = individuals.ToDictionary(individual => individual.Xref);
      _notes = notes;
      Individuals = individuals;
      Families = families;
    }

    public Individual[] Individuals { get; }
    public Family[] Families { get; }

    public static Side Build(GedcomNode[] roots)
    {
      var individuals = roots
        .Where(root => root.Tag == GedcomTags.Individual && root.Xref is not null)
        .Select(Describe)
        .ToArray();
      var families = roots.Where(root => root.Tag == GedcomTags.Family).Select(ReadFamily).ToArray();
      var noteRecords = roots.Where(root => root.Tag == GedcomTags.Note && root.Xref is not null);
      var notes = noteRecords.ToDictionary(record => record.Xref!, record => record.Value ?? string.Empty);

      var side = new Side(roots, individuals, families, notes);
      side.LinkNeighbours();
      side.CollectMedia();
      return side;
    }

    public static bool IsAdopted(Individual child, string familyXref)
    {
      var links = child.Node.ChildrenWithTag(GedcomTags.FamilyChild);
      var link = links.FirstOrDefault(node => node.Value == familyXref);
      return link?.ChildValue(GedcomTags.Pedigree) == GedcomTags.AdoptedPedigree;
    }

    public Individual? Resolve(string? xref)
    {
      if (xref is null)
        return null;

      _byXref.TryGetValue(xref, out var individual);
      return individual;
    }

    public string[] ContentTags(Individual individual)
    {
      var compared = individual.Node.Children.Where(child => !StructuralTags.Contains(child.Tag));
      var serialized = compared.Select(child => Serialize(child, _notes));
      return serialized.Where(text => text.Length > 0).ToArray();
    }

    public Passthrough[] PassthroughRecords()
    {
      var records = _roots.Where(GedcomMetadata.IsPassthrough);
      return records.Select(record => new Passthrough($"{record.Tag} {record.Xref}", Serialize(record, _notes))).ToArray();
    }

    /// <summary>
    /// Every OBJE a person owns, wherever the file put it: directly under the INDI, nested under one of
    /// their events, or on a FAM they are a spouse in -- import lands all three on the person (#148), so
    /// they have to count on both sides for the comparison to line up.
    /// </summary>
    private void CollectMedia()
    {
      foreach (var individual in Individuals)
      {
        var owned = individual.Node.DescendantsWithTag(GedcomTags.Object);
        individual.MediaTitles.AddRange(owned.SelectMany(TitlesOf));
      }

      foreach (var family in _roots.Where(root => root.Tag == GedcomTags.Family))
      {
        var media = family.DescendantsWithTag(GedcomTags.Object).SelectMany(TitlesOf).ToArray();
        if (media.Length == 0)
          continue;

        var husband = Resolve(family.ChildValue(GedcomTags.Husband));
        var wife = Resolve(family.ChildValue(GedcomTags.Wife));
        husband?.MediaTitles.AddRange(media);
        wife?.MediaTitles.AddRange(media);
      }
    }

    /// <summary>
    /// One title per referenced file: import splits a multi-FILE OBJE into one OBJE per file up front, so
    /// an OBJE naming two files is two media items on the other side.
    /// </summary>
    private static IEnumerable<string> TitlesOf(GedcomNode media)
    {
      var title = media.ChildValue(GedcomTags.Title) ?? "(untitled)";
      var files = media.ChildrenWithTag(GedcomTags.File).Count();
      return Enumerable.Repeat(title, Math.Max(1, files));
    }

    private void LinkNeighbours()
    {
      foreach (var family in Families)
      {
        var husband = Resolve(family.Husband);
        var wife = Resolve(family.Wife);
        if (husband is not null && family.Wife is not null)
        {
          husband.Neighbours.Add(("S", family.Wife));
        }
        if (wife is not null && family.Husband is not null)
        {
          wife.Neighbours.Add(("S", family.Husband));
        }
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
      var raw = EventDateValue(individual, tag);
      return CanonicalDate(raw);
    }
  }
}
