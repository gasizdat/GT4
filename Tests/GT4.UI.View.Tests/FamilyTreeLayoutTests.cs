using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using System.Diagnostics;
using Xunit;

namespace GT4.UI.View.Tests;

public class FamilyTreeLayoutTests
{
  private static readonly FamilyTreeLayoutMetrics _metrics = new();

  private static FamilyTreeLayoutResult Layout(
    FamilyTree tree,
    FamilyTreeLayoutMetrics metrics,
    IReadOnlyDictionary<int, double>? pins = null) =>
    FamilyTreeLayout.Compute(tree, metrics, pins);

  private static PersonInfo MakePerson(int id) =>
    new(id, Date.Create(0, DateStatus.Unknown), null, BiologicalSex.Unknown, [], null);

  private static FamilyTreeNode MakeNode(int id, int generation) =>
    new(MakePerson(id), generation);

  private static FamilyTree MakeTree(int centerId, FamilyTreeNode[] nodes, FamilyTreeEdge[]? edges = null) =>
    new(centerId, nodes, edges ?? []);

  private static double OffsetFromCenter(FamilyTreeLayoutResult result, int id) =>
    (result.Nodes.Single(n => n.Node.Id == id).Bounds.Left - result.CenterTopLeft.X) / _metrics.SlotPitch;

  private static FamilyTreeLayoutMetrics Zoomed(double zoom) => _metrics with
  {
    NodeWidth = _metrics.NodeWidth * zoom,
    NodeHeight = _metrics.NodeHeight * zoom,
    HorizontalGap = _metrics.HorizontalGap * zoom,
    VerticalGap = _metrics.VerticalGap * zoom,
    CornerRadius = _metrics.CornerRadius * zoom,
  };

  // Shaped like FamilyTreeProvider's output: every parent in the tree links to each of its children, and
  // a spouse shares the row of the person they married.
  private sealed class TreeBuilder
  {
    private readonly List<FamilyTreeNode> _Nodes = [];
    private readonly List<FamilyTreeEdge> _Edges = [];

    public int Person(int generation, BiologicalSex sex = BiologicalSex.Unknown, int birthYear = 0)
    {
      var id = _Nodes.Count + 1;
      var birthDate = birthYear == 0
        ? Date.Create(0, DateStatus.Unknown)
        : Date.Create(birthYear, null, null, DateStatus.MonthUnknown);
      _Nodes.Add(new FamilyTreeNode(new PersonInfo(id, birthDate, null, sex, [], null), generation));
      return id;
    }

    public void Marry(int a, int b) => _Edges.Add(FamilyTreeEdge.Spouse(a, b));

    public void Children(int[] parents, params int[] children)
    {
      foreach (var parent in parents)
        foreach (var child in children)
          _Edges.Add(FamilyTreeEdge.ParentChild(parent, child));
    }

    public FamilyTree Build(int centerId) => new(centerId, [.. _Nodes], [.. _Edges]);
  }

  private const BiologicalSex Male = BiologicalSex.Male;
  private const BiologicalSex Female = BiologicalSex.Female;

  // Centre 1 with parents 2 and 3 above and children 4 and 5 below.
  private static FamilyTree ShallowTree() => MakeTree(
    1,
    [MakeNode(1, 0), MakeNode(2, 1), MakeNode(3, 1), MakeNode(4, -1), MakeNode(5, -1)],
    [
      FamilyTreeEdge.ParentChild(parentId: 2, childId: 1),
      FamilyTreeEdge.ParentChild(parentId: 3, childId: 1),
      FamilyTreeEdge.Spouse(2, 3),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 4),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 5),
    ]);

  // ShallowTree plus a grandparent above 2 and three grandchildren under 4, which moves the centre's
  // own seed column.
  private static FamilyTree DeeperTree() => MakeTree(
    1,
    [
      MakeNode(1, 0), MakeNode(2, 1), MakeNode(3, 1), MakeNode(4, -1), MakeNode(5, -1),
      MakeNode(6, 2), MakeNode(7, -2), MakeNode(8, -2), MakeNode(9, -2),
    ],
    [
      FamilyTreeEdge.ParentChild(parentId: 2, childId: 1),
      FamilyTreeEdge.ParentChild(parentId: 3, childId: 1),
      FamilyTreeEdge.Spouse(2, 3),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 4),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 5),
      FamilyTreeEdge.ParentChild(parentId: 6, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 4, childId: 7),
      FamilyTreeEdge.ParentChild(parentId: 4, childId: 8),
      FamilyTreeEdge.ParentChild(parentId: 4, childId: 9),
    ]);

  // Ids: centre 1, spouse 2, father 3, mother 4, FF 5, FM 6, MF 7, MM 8, children K1 9, K2 10, K3 11,
  // K1's spouse 12, K1's children 13-14, K2's spouse 15, K2's child 16.
  private static FamilyTree BowTie()
  {
    var b = new TreeBuilder();
    var center = b.Person(0, Male, 1950);
    var spouse = b.Person(0, Female, 1952);
    b.Marry(center, spouse);
    var father = b.Person(1, Male, 1920);
    var mother = b.Person(1, Female, 1922);
    b.Marry(father, mother);
    b.Children([father, mother], center);
    var ff = b.Person(2, Male);
    var fm = b.Person(2, Female);
    b.Marry(ff, fm);
    b.Children([ff, fm], father);
    var mf = b.Person(2, Male);
    var mm = b.Person(2, Female);
    b.Marry(mf, mm);
    b.Children([mf, mm], mother);
    var k1 = b.Person(-1, Male, 1975);
    var k2 = b.Person(-1, Female, 1977);
    var k3 = b.Person(-1, Male, 1980);
    b.Children([center, spouse], k1, k2, k3);
    var k1Spouse = b.Person(-1, Female);
    b.Marry(k1, k1Spouse);
    b.Children([k1, k1Spouse], b.Person(-2, birthYear: 2000), b.Person(-2, birthYear: 2002));
    var k2Spouse = b.Person(-1, Male);
    b.Marry(k2, k2Spouse);
    b.Children([k2, k2Spouse], b.Person(-2));
    return b.Build(center);
  }

  private static FamilyTree TwoMarriages()
  {
    var b = new TreeBuilder();
    var center = b.Person(0, Male, 1950);
    var wife1 = b.Person(0, Female, 1951);
    var wife2 = b.Person(0, Female, 1960);
    b.Marry(center, wife1);
    b.Marry(center, wife2);
    var father = b.Person(1, Male);
    var mother = b.Person(1, Female);
    b.Marry(father, mother);
    b.Children([father, mother], center);
    b.Marry(father, b.Person(1, Female));
    b.Marry(mother, b.Person(1, Male));
    var k1 = b.Person(-1, Male, 1975);
    var k3 = b.Person(-1, Male, 1978);
    b.Children([center, wife1], k1, k3);
    var k2 = b.Person(-1, Female, 1985);
    var k4 = b.Person(-1, Female, 1988);
    b.Children([center, wife2], k2, k4);
    var k1Spouse = b.Person(-1, Female);
    b.Marry(k1, k1Spouse);
    b.Children([k1, k1Spouse], b.Person(-2));
    var k4Spouse = b.Person(-1, Male);
    b.Marry(k4, k4Spouse);
    b.Children([k4, k4Spouse], b.Person(-2), b.Person(-2));
    return b.Build(center);
  }

  // The centre, with parents above, married as many times as asked, with a child or two by each wife.
  private static FamilyTree ManyMarriages(int marriages)
  {
    var b = new TreeBuilder();
    var center = b.Person(0, Male, 1950);
    var father = b.Person(1, Male);
    var mother = b.Person(1, Female);
    b.Marry(father, mother);
    b.Children([father, mother], center);
    for (var i = 1; i <= marriages; i++)
    {
      var wife = b.Person(0, Female, 1950 + i);
      b.Marry(center, wife);
      b.Children([center, wife], b.Person(-1, birthYear: 1970 + (10 * i)));
      if (i % 2 == 0)
        b.Children([center, wife], b.Person(-1, birthYear: 1975 + (10 * i)));
    }
    return b.Build(center);
  }

  private static FamilyTree Collaterals()
  {
    var b = new TreeBuilder();
    var ff = b.Person(2, Male);
    var fm = b.Person(2, Female);
    b.Marry(ff, fm);
    var mf = b.Person(2, Male);
    var mm = b.Person(2, Female);
    b.Marry(mf, mm);
    var father = b.Person(1, Male, 1920);
    var uncle = b.Person(1, Male, 1915);
    b.Children([ff, fm], uncle, father);
    var unclesWife = b.Person(1, Female);
    b.Marry(uncle, unclesWife);
    var mother = b.Person(1, Female, 1922);
    var aunt = b.Person(1, Female, 1925);
    var uncle2 = b.Person(1, Male, 1928);
    b.Children([mf, mm], mother, aunt, uncle2);
    var auntsHusband = b.Person(1, Male);
    b.Marry(aunt, auntsHusband);
    b.Marry(father, mother);
    var center = b.Person(0, Male, 1950);
    var brother = b.Person(0, Male, 1948);
    var sister = b.Person(0, Female, 1955);
    b.Children([father, mother], brother, center, sister);
    var spouse = b.Person(0, Female);
    b.Marry(center, spouse);
    var brothersWife = b.Person(0, Female);
    b.Marry(brother, brothersWife);
    var cousin1 = b.Person(0, Male, 1945);
    var cousin2 = b.Person(0, Female, 1947);
    b.Children([uncle, unclesWife], cousin1, cousin2);
    var cousin1Wife = b.Person(0, Female);
    b.Marry(cousin1, cousin1Wife);
    b.Children([aunt, auntsHusband], b.Person(0, Male, 1951));
    var k1 = b.Person(-1, Male, 1975);
    b.Children([center, spouse], k1, b.Person(-1, Female, 1978));
    b.Children([brother, brothersWife], b.Person(-1));
    b.Children([cousin1, cousin1Wife], b.Person(-1));
    b.Children([k1], b.Person(-2));
    return b.Build(center);
  }

  private static FamilyTree WideDescendants()
  {
    var b = new TreeBuilder();
    var center = b.Person(0, Male);
    var spouse = b.Person(0, Female);
    b.Marry(center, spouse);
    for (var i = 1; i <= 5; i++)
    {
      var child = b.Person(-1, birthYear: 1970 + i);
      b.Children([center, spouse], child);
      var childsSpouse = b.Person(-1);
      b.Marry(child, childsSpouse);
      for (var j = 1; j <= (i % 3) + 1; j++)
        b.Children([child, childsSpouse], b.Person(-2, birthYear: 2000 + j));
    }
    return b.Build(center);
  }

  // A full ancestry: 2^depth people on the top row, every couple married.
  private static FamilyTree Pedigree(int depth)
  {
    var b = new TreeBuilder();
    var center = b.Person(0);
    int[] row = [center];
    for (var generation = 1; generation <= depth; generation++)
    {
      var next = new List<int>();
      foreach (var child in row)
      {
        var father = b.Person(generation, Male);
        var mother = b.Person(generation, Female);
        b.Marry(father, mother);
        b.Children([father, mother], child);
        next.Add(father);
        next.Add(mother);
      }
      row = [.. next];
    }
    return b.Build(center);
  }

  // Ids: grandparents 1-2, father 3, uncle 4, mother 5, uncle's wife 6, centre 7, sister 8, cousin 9,
  // the cousins' child 10. The sister and the cousin married each other.
  private static FamilyTree CousinMarriage()
  {
    var b = new TreeBuilder();
    var grandfather = b.Person(2, Male);
    var grandmother = b.Person(2, Female);
    b.Marry(grandfather, grandmother);
    var father = b.Person(1, Male, 1920);
    var uncle = b.Person(1, Male, 1915);
    b.Children([grandfather, grandmother], uncle, father);
    var mother = b.Person(1, Female);
    b.Marry(father, mother);
    var unclesWife = b.Person(1, Female);
    b.Marry(uncle, unclesWife);
    var center = b.Person(0, Male, 1950);
    var sister = b.Person(0, Female, 1952);
    b.Children([father, mother], center, sister);
    var cousin = b.Person(0, Male, 1948);
    b.Children([uncle, unclesWife], cousin);
    b.Marry(cousin, sister);
    b.Children([cousin, sister], b.Person(-1));
    return b.Build(center);
  }

  // With collaterals: the parents are first cousins, so the mother is also a descendant of the father's
  // grandparents. Ids: great-grandparents 1-2, the father's father 3, the mother's mother 4, father 5,
  // mother 6, centre 7.
  private static FamilyTree CousinParents()
  {
    var b = new TreeBuilder();
    var greatGrandfather = b.Person(3, Male);
    var greatGrandmother = b.Person(3, Female);
    b.Marry(greatGrandfather, greatGrandmother);
    var grandfather = b.Person(2, Male, 1890);
    var grandmother = b.Person(2, Female, 1895);
    b.Children([greatGrandfather, greatGrandmother], grandfather, grandmother);
    var father = b.Person(1, Male);
    b.Children([grandfather], father);
    var mother = b.Person(1, Female);
    b.Children([grandmother], mother);
    b.Marry(father, mother);
    var center = b.Person(0);
    b.Children([father, mother], center);
    return b.Build(center);
  }

  // Ids: centre 1, father 2, mother 3, grandparents 4-5. The parents have no spouse edge.
  private static FamilyTree CoParents()
  {
    var b = new TreeBuilder();
    var center = b.Person(0);
    var father = b.Person(1, Male);
    var mother = b.Person(1, Female);
    b.Children([father, mother], center);
    var grandfather = b.Person(2, Male);
    var grandmother = b.Person(2, Female);
    b.Marry(grandfather, grandmother);
    b.Children([grandfather, grandmother], father);
    return b.Build(center);
  }

  // Ids: centre 1, mother 2, children 3-4, one under each parent.
  private static FamilyTree TwoChildren(bool married)
  {
    var b = new TreeBuilder();
    var center = b.Person(1, Male);
    var mother = b.Person(1, Female);
    if (married)
      b.Marry(center, mother);
    b.Children([center, mother], b.Person(0, birthYear: 1950), b.Person(0, birthYear: 1952));
    return b.Build(center);
  }

  // Ids: centre 1, his first partner 2, their children 3-4, his second partner 5, their child 6. He married
  // neither.
  private static FamilyTree UnmarriedPartners()
  {
    var b = new TreeBuilder();
    var center = b.Person(0, Male);
    var first = b.Person(0, Female);
    b.Children([center, first], b.Person(-1, birthYear: 1970), b.Person(-1, birthYear: 1972));
    var second = b.Person(0, Female);
    b.Children([center, second], b.Person(-1, birthYear: 1980));
    return b.Build(center);
  }

  // Ids: centre 1, father 2, mother 3, adoptive father 4, adoptive mother 5, centre's child 6. All four
  // parents are in the row above.
  private static FamilyTree ManyParents()
  {
    var b = new TreeBuilder();
    var center = b.Person(0);
    var father = b.Person(1, Male);
    var mother = b.Person(1, Female);
    b.Marry(father, mother);
    var adoptiveFather = b.Person(1, Male);
    var adoptiveMother = b.Person(1, Female);
    b.Marry(adoptiveFather, adoptiveMother);
    b.Children([father, mother, adoptiveFather, adoptiveMother], center);
    b.Children([center], b.Person(-1));
    return b.Build(center);
  }

  // What the provider emits for pedigree collapse. Ids: centre 1, father 2, mother 3, X 4 (the father's
  // father), X's wife 5, Y 6 (the mother's father, and X's son), Y's wife 7, Z 8 (X's second wife and
  // Y's mother), X's parents 9-10. X and Y share a row, and X married Z a row above him.
  private static FamilyTree PedigreeCollapse()
  {
    var b = new TreeBuilder();
    var center = b.Person(0);
    var father = b.Person(1, Male);
    var mother = b.Person(1, Female);
    b.Marry(father, mother);
    b.Children([father, mother], center);
    var x = b.Person(2, Male);
    var xWife = b.Person(2, Female);
    b.Marry(x, xWife);
    b.Children([x, xWife], father);
    var y = b.Person(2, Male);
    var yWife = b.Person(2, Female);
    b.Marry(y, yWife);
    b.Children([y, yWife], mother);
    var z = b.Person(3, Female);
    b.Children([x, z], y);
    b.Marry(x, z);
    var xFather = b.Person(3, Male);
    var xMother = b.Person(3, Female);
    b.Marry(xFather, xMother);
    b.Children([xFather, xMother], x);
    return b.Build(center);
  }

  private static readonly Dictionary<string, Func<FamilyTree>> _fixtures = new()
  {
    ["bow-tie"] = BowTie,
    ["two marriages"] = TwoMarriages,
    ["three marriages"] = () => ManyMarriages(3),
    ["four marriages"] = () => ManyMarriages(4),
    ["collaterals"] = Collaterals,
    ["wide descendants"] = WideDescendants,
    ["cousin marriage"] = CousinMarriage,
    ["co-parents"] = CoParents,
    ["unmarried partners"] = UnmarriedPartners,
    ["unmarried over two"] = () => TwoChildren(married: false),
    ["many parents"] = ManyParents,
    ["pedigree collapse"] = PedigreeCollapse,
    ["cousin parents"] = CousinParents,
    ["pedigree of 4"] = () => Pedigree(4),
    ["pedigree of 5"] = () => Pedigree(5),
    ["pedigree of 6"] = () => Pedigree(6),
  };

  public static TheoryData<string, double> FixturesAtZooms()
  {
    var data = new TheoryData<string, double>();
    foreach (var name in _fixtures.Keys)
      foreach (var zoom in new[] { 1.0, 0.75, 0.4 })
        data.Add(name, zoom);
    return data;
  }

  // ── criteria ─────────────────────────────────────────────────────────────────────────────────────

  private readonly record struct Segment(int Connector, bool Horizontal, double Fixed, double From, double To);

  private static Segment[] Segments(FamilyTreeLayoutResult result)
  {
    var segments = new List<Segment>();
    for (var c = 0; c < result.Connectors.Count; c++)
    {
      var points = result.Connectors[c].Points;
      for (var i = 1; i < points.Length; i++)
      {
        var a = points[i - 1];
        var z = points[i];
        if (a == z)
          continue;
        var horizontal = Math.Abs(a.Y - z.Y) < 1e-6;
        var skew = horizontal ? a.Y - z.Y : a.X - z.X;
        Math.Abs(skew).Should().BeLessThan(1e-6f, "connector {0} must be orthogonal", c);
        segments.Add(horizontal
          ? new Segment(c, true, a.Y, Math.Min(a.X, z.X), Math.Max(a.X, z.X))
          : new Segment(c, false, a.X, Math.Min(a.Y, z.Y), Math.Max(a.Y, z.Y)));
      }
    }
    return [.. segments];
  }

  private static void AssertDrawnOnce(Segment[] segments)
  {
    foreach (var a in segments)
      foreach (var b in segments.Where(s => s.Connector > a.Connector && s.Horizontal == a.Horizontal))
      {
        if (Math.Abs(a.Fixed - b.Fixed) >= 1)
          continue;
        var overlap = Math.Min(a.To, b.To) - Math.Max(a.From, b.From);
        overlap.Should().BeLessThan(0.5, "connectors {0} and {1} must not stroke the same line", a.Connector, b.Connector);
      }
  }

  // Two runs on one line read as one unless a real gap parts them. The exceptions are a T-junction, where
  // both runs end on a vertical, and two lines meeting behind a node, as a person between two spouses.
  private static void AssertNoMerges(FamilyTreeLayoutResult result, Segment[] segments, FamilyTreeLayoutMetrics metrics)
  {
    var horizontals = segments.Where(s => s.Horizontal).ToArray();
    var verticals = segments.Where(s => !s.Horizontal).ToArray();
    foreach (var a in horizontals)
      foreach (var b in horizontals.Where(s => s.Connector > a.Connector && Math.Abs(s.Fixed - a.Fixed) < 1))
      {
        var (left, right) = a.From <= b.From ? (a, b) : (b, a);
        var gap = right.From - left.To;
        if (gap >= metrics.HorizontalGap / 2)
          continue;
        var touch = left.To;
        var onVertical = verticals.Any(v => Math.Abs(v.Fixed - touch) < 1e-6 && v.From <= a.Fixed + 1e-6 && v.To >= a.Fixed - 1e-6);
        var behindNode = result.Nodes.Any(n => Inside(n.Bounds, touch, a.Fixed));
        var atJunction = Math.Abs(gap) < 1e-6 && (onVertical || behindNode);
        atJunction.Should().BeTrue("connectors {0} and {1} run along y={2} without a gap", a.Connector, b.Connector, a.Fixed);
      }
  }

  private static void AssertNoShortJogs(FamilyTreeLayoutResult result, Segment[] segments, FamilyTreeLayoutMetrics metrics)
  {
    var lines = segments.Where(s =>
    {
      var connector = result.Connectors[s.Connector];
      return s.Horizontal && connector.Relation == FamilyTreeRelation.ParentChild && !connector.IsLoop;
    });
    foreach (var s in lines)
      (s.To - s.From).Should().BeGreaterThanOrEqualTo(2 * metrics.CornerRadius - 1e-6, "connector {0} has a jog", s.Connector);
  }

  private static bool Inside(Rect r, double x, double y) =>
    x > r.Left + 0.5 && x < r.Right - 0.5 && y > r.Top + 0.5 && y < r.Bottom - 0.5;

  // A segment may end inside the node it joins, where the node's own photo covers it.
  private static void AssertNothingThroughANode(FamilyTreeLayoutResult result, Segment[] segments)
  {
    foreach (var node in result.Nodes)
    {
      var r = node.Bounds;
      foreach (var s in segments)
      {
        var endsHere = s.Horizontal
          ? Inside(r, s.From, s.Fixed) || Inside(r, s.To, s.Fixed)
          : Inside(r, s.Fixed, s.From) || Inside(r, s.Fixed, s.To);
        if (endsHere)
          continue;
        var crosses = s.Horizontal
          ? s.Fixed > r.Top + 0.5 && s.Fixed < r.Bottom - 0.5 && s.To > r.Left + 0.5 && s.From < r.Right - 0.5
          : s.Fixed > r.Left + 0.5 && s.Fixed < r.Right - 0.5 && s.To > r.Top + 0.5 && s.From < r.Bottom - 0.5;
        crosses.Should().BeFalse("connector {0} must not pass through node {1}", s.Connector, node.Node.Id);
      }
    }
  }

  private static void AssertWholeUnitCentres(FamilyTreeLayoutResult result)
  {
    foreach (var node in result.Nodes)
    {
      var x = node.Bounds.Center.X;
      x.Should().Be(Math.Round(x), "node {0} must be centred on a whole unit", node.Node.Id);
    }
  }

  private static void AssertInsideCanvas(FamilyTreeLayoutResult result)
  {
    var points = result.Connectors.SelectMany(c => c.Points);
    foreach (var p in points)
    {
      p.X.Should().BeInRange(0, (float)result.CanvasSize.Width);
      p.Y.Should().BeInRange(0, (float)result.CanvasSize.Height);
    }
  }

  private static void AssertNoRowOverlaps(FamilyTreeLayoutResult result)
  {
    foreach (var row in result.Nodes.GroupBy(n => n.Node.Generation))
    {
      var ordered = row.Select(n => n.Bounds).OrderBy(b => b.Left).ToArray();
      for (var i = 1; i < ordered.Length; i++)
        ordered[i].Left.Should().BeGreaterThanOrEqualTo(ordered[i - 1].Right - 0.001, $"row {row.Key} must not overlap");
    }
  }

  private static double CentreX(FamilyTreeLayoutResult result, int id) =>
    result.Nodes.Single(n => n.Node.Id == id).Bounds.Center.X;

  private static void AssertUnderParents(FamilyTreeLayoutResult result, int childId, params int[] parentIds)
  {
    var parents = parentIds.Select(id => CentreX(result, id)).ToArray();
    var drop = (parents.Min() + parents.Max()) / 2;
    CentreX(result, childId).Should().BeApproximately(drop, 0.5, "child {0} must sit under its parents", childId);
  }

  private static bool Joins(FamilyTreeLayoutResult result, FamilyTreeConnector connector, int a, int b)
  {
    bool Touches(PointF point, int id)
    {
      var r = result.Nodes.Single(n => n.Node.Id == id).Bounds;
      return point.X >= r.Left - 1e-3 && point.X <= r.Right + 1e-3 && point.Y >= r.Top - 1e-3 && point.Y <= r.Bottom + 1e-3;
    }
    var (first, last) = (connector.Points[0], connector.Points[^1]);
    return (Touches(first, a) && Touches(last, b)) || (Touches(first, b) && Touches(last, a));
  }

  private static (FamilyTreeRelation, bool, PointF[])[] Drawing(FamilyTreeLayoutResult result) =>
    [.. result.Connectors.Select(c => (c.Relation, c.IsLoop, c.Points))];

  private static FamilyTree Reversed(FamilyTree tree)
  {
    FamilyTreeNode[] nodes = [.. tree.Nodes.Reverse()];
    FamilyTreeEdge[] edges = [.. tree.Edges.Reverse()];
    return new FamilyTree(tree.CenterId, nodes, edges);
  }

  [Theory]
  [MemberData(nameof(FixturesAtZooms))]
  public void Compute_DrawsATidyTree(string fixture, double zoom)
  {
    var metrics = Zoomed(zoom);
    var tree = _fixtures[fixture]();

    var result = Layout(tree, metrics);

    var segments = Segments(result);
    AssertDrawnOnce(segments);
    AssertNoMerges(result, segments, metrics);
    AssertNoShortJogs(result, segments, metrics);
    AssertNothingThroughANode(result, segments);
    AssertWholeUnitCentres(result);
    AssertInsideCanvas(result);
    AssertNoRowOverlaps(result);
  }

  [Theory]
  [MemberData(nameof(FixturesAtZooms))]
  public void Compute_DoesNotDependOnTheInputOrder(string fixture, double zoom)
  {
    var metrics = Zoomed(zoom);
    var tree = _fixtures[fixture]();

    var result = Layout(tree, metrics);
    var reversed = Layout(Reversed(tree), metrics);

    var bounds = result.Nodes.OrderBy(n => n.Node.Id).Select(n => n.Bounds);
    reversed.Nodes.OrderBy(n => n.Node.Id).Select(n => n.Bounds).Should().Equal(bounds);
    Drawing(reversed).Should().BeEquivalentTo(Drawing(result), options => options.WithStrictOrdering());
  }

  [Fact]
  public void Compute_BowTie_PutsEveryChildUnderItsParents()
  {
    var result = Layout(BowTie(), _metrics);

    AssertUnderParents(result, 1, 3, 4);
    AssertUnderParents(result, 3, 5, 6);
    AssertUnderParents(result, 4, 7, 8);
    AssertUnderParents(result, 16, 10, 15);
    var children = new[] { 9, 10, 11 }.Select(id => CentreX(result, id)).ToArray();
    var couple = (CentreX(result, 1) + CentreX(result, 2)) / 2;
    ((children.Min() + children.Max()) / 2).Should().BeApproximately(couple, 0.5);
  }

  [Fact]
  public void Compute_Pedigree_PutsEveryChildUnderItsParents()
  {
    var tree = Pedigree(6);

    var result = Layout(tree, _metrics);

    var parentsOf = tree.Edges
      .Where(e => e.Relation == FamilyTreeRelation.ParentChild)
      .GroupBy(e => e.ToId);
    foreach (var parents in parentsOf)
      AssertUnderParents(result, parents.Key, [.. parents.Select(e => e.FromId)]);
  }

  [Fact]
  public void Compute_OrdersSiblingsByBirth()
  {
    var b = new TreeBuilder();
    var parent = b.Person(1);
    var middle = b.Person(0, birthYear: 1950);
    var eldest = b.Person(0, birthYear: 1940);
    var youngest = b.Person(0, birthYear: 1960);
    b.Children([parent], middle, eldest, youngest);

    var result = Layout(b.Build(parent), _metrics);

    var order = new[] { eldest, middle, youngest }.Select(id => CentreX(result, id));
    order.Should().BeInAscendingOrder();
  }

  [Fact]
  public void Compute_SetsCoParentsSideBySideWithoutASpouseLine()
  {
    var result = Layout(CoParents(), _metrics);

    Math.Abs(CentreX(result, 2) - CentreX(result, 3)).Should().Be(_metrics.SlotPitch);
    AssertUnderParents(result, 1, 2, 3);
    var spouseLines = result.Connectors.Where(c => c.Relation == FamilyTreeRelation.Spouse);
    spouseLines.Should().ContainSingle("only the grandparents are married");
    var segments = Segments(result);
    var middle = result.Nodes.Single(n => n.Node.Id == 2).Bounds.Center.Y;
    segments.Should().NotContain(s => s.Horizontal && s.Fixed == middle, "a line between the parents would claim a marriage");
    foreach (var parent in new[] { 2, 3 })
    {
      var bounds = result.Nodes.Single(n => n.Node.Id == parent).Bounds;
      segments.Should().Contain(
        s => !s.Horizontal && s.Fixed == bounds.Center.X && s.From == bounds.Bottom,
        "parent {0} drops to the child's bar on their own",
        parent);
    }
  }

  [Fact]
  public void Compute_UnmarriedParents_EachDropStraightToTheChildUnderThem()
  {
    var result = Layout(TwoChildren(married: false), _metrics);

    var segments = Segments(result);
    foreach (var parent in new[] { 1, 2 })
    {
      var bounds = result.Nodes.Single(n => n.Node.Id == parent).Bounds;
      var child = result.Nodes.Single(n => n.Node.Generation == 0 && n.Bounds.Center.X == bounds.Center.X).Bounds;
      segments.Should().Contain(
        s => !s.Horizontal && s.Fixed == bounds.Center.X && s.From == bounds.Bottom && s.To == child.Top,
        "parent {0} runs straight through the bar to the child under them",
        parent);
    }
  }

  [Fact]
  public void Compute_MarriedParents_DropOnceFromTheMiddleOfTheirMarriageLine()
  {
    var result = Layout(TwoChildren(married: true), _metrics);

    var parents = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var fromTheirRow = Segments(result).Where(s => !s.Horizontal && s.From < parents.Bottom);
    var drop = fromTheirRow.Should().ContainSingle("a married couple shares one drop").Subject;
    drop.Fixed.Should().Be((CentreX(result, 1) + CentreX(result, 2)) / 2);
    drop.From.Should().Be(parents.Center.Y);
  }

  [Fact]
  public void Compute_KeepsEachOfAChildsCouplesSideBySide()
  {
    var result = Layout(ManyParents(), _metrics);

    Math.Abs(CentreX(result, 2) - CentreX(result, 3)).Should().Be(_metrics.SlotPitch);
    Math.Abs(CentreX(result, 4) - CentreX(result, 5)).Should().Be(_metrics.SlotPitch);
    var spouseLines = result.Connectors.Where(c => c.Relation == FamilyTreeRelation.Spouse);
    spouseLines.Should().OnlyContain(c => c.Points.Length == 2, "neither marriage line has to go round a node");
  }

  [Fact]
  public void Compute_SetsMarriedCousinsAtTheFacingEndsOfTheirSibships()
  {
    var result = Layout(CousinMarriage(), _metrics);

    Math.Abs(CentreX(result, 8) - CentreX(result, 9)).Should().Be(_metrics.SlotPitch);
    result.Connectors.Count(c => c.IsLoop).Should().Be(0);
    AssertUnderParents(result, 10, 8, 9);
  }

  [Fact]
  public void Compute_PedigreeCollapse_DrawsTheRelationsTheRowsCannotHoldAsLoops()
  {
    var result = Layout(PedigreeCollapse(), _metrics);

    var loops = result.Connectors.Where(c => c.IsLoop).ToArray();
    loops.Should().HaveCount(2);
    loops.Where(c => c.Relation == FamilyTreeRelation.ParentChild && Joins(result, c, 4, 6)).Should().ContainSingle();
    loops.Where(c => c.Relation == FamilyTreeRelation.Spouse && Joins(result, c, 4, 8)).Should().ContainSingle();
  }

  [Fact]
  public void Compute_ParentsWhoAreCousins_KeepTheirOwnSidesOverTheirChild()
  {
    var result = Layout(CousinParents(), _metrics);

    CentreX(result, 5).Should().BeLessThan(CentreX(result, 6));
    AssertUnderParents(result, 7, 5, 6);
    AssertUnderParents(result, 6, 4);
    var loop = result.Connectors.Where(c => c.IsLoop).Should().ContainSingle().Subject;
    (Joins(result, loop, 1, 4) || Joins(result, loop, 2, 4)).Should().BeTrue("the mother's mother hangs from her own parents by the loop");
  }

  [Fact]
  public void Compute_LoadingMoreGenerationsKeepsTheOrderOfEachRow()
  {
    var small = Layout(Pedigree(2), _metrics);
    var large = Layout(Pedigree(5), _metrics);

    foreach (var row in small.Nodes.GroupBy(n => n.Node.Generation))
    {
      var order = row.OrderBy(n => n.Bounds.Left).Select(n => n.Node.Id);
      var ids = row.Select(n => n.Node.Id).ToHashSet();
      var orderNow = large.Nodes.Where(n => ids.Contains(n.Node.Id)).OrderBy(n => n.Bounds.Left).Select(n => n.Node.Id);
      orderNow.Should().Equal(order);
    }
  }

  [Fact]
  public void Compute_LaysOutAThousandPeopleQuickly()
  {
    var tree = Pedigree(9);
    Layout(tree, _metrics);

    var stopwatch = Stopwatch.StartNew();
    Layout(tree, _metrics);
    stopwatch.Stop();

    stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
  }

  [Fact]
  public void Compute_APinMovesOnlyThatNode()
  {
    var tree = BowTie();
    var free = Layout(tree, _metrics);

    var pinned = Layout(tree, _metrics, new Dictionary<int, double> { [10] = 6 });

    foreach (var row in free.Nodes.GroupBy(n => n.Node.Generation))
    {
      var order = row.Where(n => n.Node.Id != 10).OrderBy(n => n.Bounds.Left).Select(n => n.Node.Id);
      var ids = order.ToHashSet();
      var orderNow = pinned.Nodes.Where(n => ids.Contains(n.Node.Id)).OrderBy(n => n.Bounds.Left).Select(n => n.Node.Id);
      orderNow.Should().Equal(order);
    }
    OffsetFromCenter(pinned, 10).Should().BeApproximately(6, 1e-6);
  }

  // ── geometry basics ───────────────────────────────────────────────────────────────────────────────

  [Fact]
  public void Metrics_SlotPitch_IsNodeWidthPlusHorizontalGap()
  {
    var m = new FamilyTreeLayoutMetrics(NodeWidth: 100, HorizontalGap: 20);
    m.SlotPitch.Should().Be(120);
  }

  [Fact]
  public void Metrics_RowPitch_IsNodeHeightPlusVerticalGap()
  {
    var m = new FamilyTreeLayoutMetrics(NodeHeight: 80, VerticalGap: 40);
    m.RowPitch.Should().Be(120);
  }

  [Fact]
  public void Compute_NullTree_Throws()
  {
    var act = () => Layout(null!, _metrics);
    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Compute_NullMetrics_Throws()
  {
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var act = () => Layout(tree, null!);
    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Compute_EmptyTree_ReturnsEmptyResult()
  {
    var result = Layout(FamilyTree.Empty, _metrics);
    result.Nodes.Should().BeEmpty();
    result.Connectors.Should().BeEmpty();
    result.CanvasSize.Width.Should().Be(0);
    result.CanvasSize.Height.Should().Be(0);
  }

  [Fact]
  public void Compute_SingleNode_ReturnsOneNodeAndNoConnectors()
  {
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = Layout(tree, _metrics);
    result.Nodes.Should().HaveCount(1);
    result.Connectors.Should().BeEmpty();
  }

  [Fact]
  public void Compute_SingleNode_NodeHasMetricsDimensions()
  {
    var m = new FamilyTreeLayoutMetrics(NodeWidth: 100, NodeHeight: 80);
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = Layout(tree, m);
    result.Nodes[0].Bounds.Width.Should().Be(100);
    result.Nodes[0].Bounds.Height.Should().Be(80);
  }

  [Fact]
  public void Compute_SingleNode_CenterTopLeftMatchesNodeTopLeft()
  {
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = Layout(tree, _metrics);
    result.CenterTopLeft.X.Should().Be(result.Nodes[0].Bounds.Left);
    result.CenterTopLeft.Y.Should().Be(result.Nodes[0].Bounds.Top);
  }

  [Fact]
  public void Compute_SingleNode_CanvasIsAtLeastNodePlusTwoMargins()
  {
    var m = new FamilyTreeLayoutMetrics(NodeWidth: 100, NodeHeight: 80, Margin: 20);
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = Layout(tree, m);
    result.CanvasSize.Width.Should().BeGreaterThanOrEqualTo(100 + 2 * 20);
    result.CanvasSize.Height.Should().BeGreaterThanOrEqualTo(80 + 2 * 20);
  }

  [Fact]
  public void Compute_AncestorIsAboveCenter()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    var center = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var ancestor = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    ancestor.Top.Should().BeLessThan(center.Top, "ancestors draw above the center person");
  }

  [Fact]
  public void Compute_DescendantIsBelowCenter()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, -1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 1, childId: 2) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    var center = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var descendant = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    descendant.Top.Should().BeGreaterThan(center.Top, "descendants draw below the center person");
  }

  [Fact]
  public void Compute_SpousesHaveSameVerticalPosition()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    var a = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var b = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    a.Top.Should().Be(b.Top);
  }

  [Fact]
  public void Compute_ConsecutiveGenerations_VerticalSpacingEqualsRowPitch()
  {
    var m = new FamilyTreeLayoutMetrics(NodeHeight: 80, VerticalGap: 40);
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = Layout(MakeTree(1, nodes, edges), m);

    var center = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var ancestor = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    (center.Top - ancestor.Top).Should().BeApproximately(m.RowPitch, precision: 0.001);
  }

  [Fact]
  public void Compute_ThreeGenerations_EachRowAtCorrectVerticalStep()
  {
    var m = new FamilyTreeLayoutMetrics(NodeHeight: 80, VerticalGap: 40);
    // gen 2 → gen 1 → gen 0
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1), MakeNode(3, 2) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 3, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 2, childId: 1),
    };
    var result = Layout(MakeTree(1, nodes, edges), m);

    var row0 = result.Nodes.Single(n => n.Node.Id == 1).Bounds.Top;
    var row1 = result.Nodes.Single(n => n.Node.Id == 2).Bounds.Top;
    var row2 = result.Nodes.Single(n => n.Node.Id == 3).Bounds.Top;

    (row0 - row1).Should().BeApproximately(m.RowPitch, 0.001);
    (row1 - row2).Should().BeApproximately(m.RowPitch, 0.001);
  }

  [Fact]
  public void Compute_SiblingNodes_DoNotHorizontallyOverlap()
  {
    var nodes = new[] { MakeNode(1, 1), MakeNode(2, 0), MakeNode(3, 0) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 3),
    };
    var result = Layout(MakeTree(2, nodes, edges), _metrics);

    var ordered = result.Nodes
      .Where(n => n.Node.Generation == 0)
      .Select(n => n.Bounds)
      .OrderBy(b => b.Left)
      .ToList();
    ordered[0].Right.Should().BeLessThanOrEqualTo(ordered[1].Left, "siblings must not overlap horizontally");
  }

  [Fact]
  public void Compute_TwoSiblingsUnderParent_ParentBetweenThem()
  {
    var nodes = new[] { MakeNode(1, 1), MakeNode(2, 0), MakeNode(3, 0) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 3),
    };
    var result = Layout(MakeTree(2, nodes, edges), _metrics);

    var parent = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var children = result.Nodes
      .Where(n => n.Node.Generation == 0)
      .Select(n => n.Bounds)
      .ToList();

    var leftmostLeft = children.Min(b => b.Left);
    var rightmostRight = children.Max(b => b.Right);

    parent.Center.X.Should().BeGreaterThanOrEqualTo(leftmostLeft);
    parent.Center.X.Should().BeLessThanOrEqualTo(rightmostRight);
  }

  [Fact]
  public void Compute_FourSiblingsOnSameRow_NoneOverlap()
  {
    var nodes = new[] { MakeNode(0, 1), MakeNode(1, 0), MakeNode(2, 0), MakeNode(3, 0), MakeNode(4, 0) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 1),
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 3),
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 4),
    };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    var row = result.Nodes
      .Where(n => n.Node.Generation == 0)
      .Select(n => n.Bounds)
      .OrderBy(b => b.Left)
      .ToList();

    for (var i = 1; i < row.Count; i++)
      row[i].Left.Should().BeGreaterThanOrEqualTo(row[i - 1].Right, "no two siblings should overlap");
  }

  [Fact]
  public void Compute_AnOnlyChildOfOneParent_HangsOnOneStraightLine()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    result.Connectors.Single(c => c.Relation == FamilyTreeRelation.ParentChild)
      .Points.Should().HaveCount(2);
  }

  [Fact]
  public void Compute_SpouseEdge_ConnectorHasTwoPoints()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    result.Connectors.Single(c => c.Relation == FamilyTreeRelation.Spouse)
      .Points.Should().HaveCount(2, "spouse uses a 2-point straight horizontal segment");
  }

  [Fact]
  public void Compute_ParentChildConnector_StartsAtParentBottomCenterEndsAtChildTopCenter()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    var parent = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    var child = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var pts = result.Connectors.Single(c => c.Relation == FamilyTreeRelation.ParentChild).Points;

    pts[0].X.Should().BeApproximately((float)parent.Center.X, 0.01f);
    pts[0].Y.Should().BeApproximately((float)parent.Bottom, 0.01f);
    pts[^1].X.Should().BeApproximately((float)child.Center.X, 0.01f);
    pts[^1].Y.Should().BeApproximately((float)child.Top, 0.01f);
  }

  [Fact]
  public void Compute_SpouseConnector_IsHorizontal()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    var pts = result.Connectors.Single(c => c.Relation == FamilyTreeRelation.Spouse).Points;
    pts[0].Y.Should().BeApproximately(pts[1].Y, 0.01f, "spouse connector is a horizontal line");
  }

  [Fact]
  public void Compute_SpouseConnector_RunsBetweenThePartnersCentres()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    var boundsById = result.Nodes.ToDictionary(n => n.Node.Id, n => n.Bounds);
    var left = boundsById.Values.MinBy(b => b.Left)!;
    var right = boundsById.Values.MaxBy(b => b.Left)!;
    var pts = result.Connectors.Single(c => c.Relation == FamilyTreeRelation.Spouse).Points;

    var leftX = Math.Min(pts[0].X, pts[1].X);
    var rightX = Math.Max(pts[0].X, pts[1].X);
    ((double)leftX).Should().BeApproximately(left.Center.X, 0.01);
    ((double)rightX).Should().BeApproximately(right.Center.X, 0.01);
  }

  [Fact]
  public void Compute_EdgeCountMatchesConnectorCount()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0), MakeNode(3, 1) };
    var edges = new[]
    {
      FamilyTreeEdge.Spouse(1, 2),
      FamilyTreeEdge.ParentChild(parentId: 3, childId: 1),
    };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);
    result.Connectors.Should().HaveCount(2);
  }

  [Fact]
  public void Compute_AllNodeBoundsAreWithinCanvasSize()
  {
    var nodes = new[]
    {
      MakeNode(1, 0), MakeNode(2, 0), MakeNode(3, 1), MakeNode(4, -1),
    };
    var edges = new[]
    {
      FamilyTreeEdge.Spouse(1, 2),
      FamilyTreeEdge.ParentChild(parentId: 3, childId: 1),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 4),
    };
    var result = Layout(MakeTree(1, nodes, edges), _metrics);

    foreach (var nodeLayout in result.Nodes)
    {
      nodeLayout.Bounds.Right.Should().BeLessThanOrEqualTo(result.CanvasSize.Width,
        $"node {nodeLayout.Node.Id} right edge must fit within canvas width");
      nodeLayout.Bounds.Bottom.Should().BeLessThanOrEqualTo(result.CanvasSize.Height,
        $"node {nodeLayout.Node.Id} bottom edge must fit within canvas height");
    }
  }

  [Fact]
  public void Compute_CenterTopLeft_MatchesCenterNodeBoundsTopLeft()
  {
    var nodes = new[] { MakeNode(10, 0), MakeNode(20, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 20, childId: 10) };
    var result = Layout(MakeTree(10, nodes, edges), _metrics);

    var centerNode = result.Nodes.Single(n => n.Node.Id == 10).Bounds;
    result.CenterTopLeft.X.Should().Be(centerNode.Left);
    result.CenterTopLeft.Y.Should().Be(centerNode.Top);
  }

  [Fact]
  public void Compute_MultipleNodesIncludingCenter_CenterTopLeftMatchesCenterNode()
  {
    // Center (id 5) is flanked by a sibling (id 6) and topped by a parent (id 7).
    var nodes = new[] { MakeNode(5, 0), MakeNode(6, 0), MakeNode(7, 1) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 7, childId: 5),
      FamilyTreeEdge.ParentChild(parentId: 7, childId: 6),
    };
    var result = Layout(MakeTree(5, nodes, edges), _metrics);

    var centerBounds = result.Nodes.Single(n => n.Node.Id == 5).Bounds;
    result.CenterTopLeft.X.Should().Be(centerBounds.Left);
    result.CenterTopLeft.Y.Should().Be(centerBounds.Top);
  }

  // ── pins ──────────────────────────────────────────────────────────────────────────────────────────

  [Fact]
  public void Compute_PinnedNode_SitsAtItsOffsetFromTheCenter()
  {
    var tree = ShallowTree();

    var result = Layout(tree, _metrics, new Dictionary<int, double> { [4] = -3.5 });

    OffsetFromCenter(result, 4).Should().BeApproximately(-3.5, 1e-6);
  }

  [Fact]
  public void Compute_Pin_HoldsInADeeperTree()
  {
    var tree = DeeperTree();
    var pins = new Dictionary<int, double> { [2] = 2.25, [5] = -4 };

    var reopened = Layout(tree, _metrics, pins);

    OffsetFromCenter(reopened, 2).Should().BeApproximately(2.25, 1e-6);
    OffsetFromCenter(reopened, 5).Should().BeApproximately(-4, 1e-6);
  }

  [Fact]
  public void Compute_FreeNodes_KeepClearOfPinnedOnes()
  {
    // One child pinned almost under the centre, where its free sibling would sit, and a grandchild
    // pinned between its two free siblings.
    var tree = DeeperTree();
    var pins = new Dictionary<int, double> { [4] = 0.3, [8] = 0 };

    var result = Layout(tree, _metrics, pins);

    AssertNoRowOverlaps(result);
  }

  [Fact]
  public void Compute_TwoPinsOnOneSlot_EndUpAWholeSlotApart()
  {
    var tree = ShallowTree();
    var pins = new Dictionary<int, double> { [4] = 2, [5] = 2 };

    var result = Layout(tree, _metrics, pins);

    OffsetFromCenter(result, 4).Should().BeApproximately(2, 1e-6);
    AssertNoRowOverlaps(result);
  }

  [Fact]
  public void Compute_AFreeNodeSkipsAGapBetweenPinsTooNarrowForIt()
  {
    var tree = DeeperTree();
    var pins = new Dictionary<int, double> { [7] = -0.5, [9] = 1 };

    var result = Layout(tree, _metrics, pins);

    AssertNoRowOverlaps(result);
    OffsetFromCenter(result, 7).Should().BeApproximately(-0.5, 1e-6);
    OffsetFromCenter(result, 9).Should().BeApproximately(1, 1e-6);
  }

  [Fact]
  public void Compute_PinOfANodeNotInTheTree_IsIgnored()
  {
    var tree = ShallowTree();
    var pinned = Layout(tree, _metrics, new Dictionary<int, double> { [99] = 3 });
    var fresh = Layout(tree, _metrics);

    var freshBounds = fresh.Nodes.Select(n => n.Bounds);
    pinned.Nodes.Select(n => n.Bounds).Should().Equal(freshBounds);
  }

  [Theory]
  [InlineData(3.0, 3.0)]   // already clear
  [InlineData(0.4, 1.0)]   // blocked, right of the pin
  [InlineData(-0.4, -1.0)] // blocked, left of the pin
  [InlineData(0.9, 1.0)]   // just short of clear
  public void NearestClearSlot_MovesOnlyAsFarAsTheNearestClearSide(double slot, double expected)
  {
    FamilyTreeLayout.NearestClearSlot(slot, [0]).Should().BeApproximately(expected, 1e-9);
  }

  [Fact]
  public void NearestClearSlot_SkipsAGapTooNarrowForANode()
  {
    // 0 and 1.5 leave no whole slot between them, so the nearest clear side is outside the pair.
    FamilyTreeLayout.NearestClearSlot(0.7, [0, 1.5]).Should().BeApproximately(-1, 1e-9);
  }

  [Fact]
  public void NearestClearSlot_NeverGoesBelowTheMinimum()
  {
    FamilyTreeLayout.NearestClearSlot(-0.4, [0], minimum: -0.5).Should().BeApproximately(1, 1e-9);
  }

  [Fact]
  public void NearestClearSlot_BelowTheMinimum_RisesToIt()
  {
    // Every neighbour of the taken slot is below the minimum.
    var clear = FamilyTreeLayout.NearestClearSlot(-5, [-3], minimum: 0);

    clear.Should().BeApproximately(0, 1e-9);
  }
}
