using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Genealogy;
using Xunit;

namespace GT4.UI.View.Tests;

public class FanChartLayoutTests
{
  private const double Tolerance = 1e-9;
  private const BiologicalSex Male = BiologicalSex.Male;
  private const BiologicalSex Female = BiologicalSex.Female;
  private const BiologicalSex Unknown = BiologicalSex.Unknown;

  private static readonly FanChartMetrics _metrics = new();

  public static TheoryData<double> Sweeps => [180, 270, 360];

  // Shaped like FamilyTreeProvider's ancestor-only output: every in-tree parent links to its child.
  private sealed class TreeBuilder
  {
    private readonly List<FamilyTreeNode> _Nodes = [];
    private readonly List<FamilyTreeEdge> _Edges = [];

    public int Person(BiologicalSex sex = Unknown)
    {
      var id = _Nodes.Count + 1;
      var person = new PersonInfo(id, Date.Create(0, DateStatus.Unknown), null, sex, [], null);
      _Nodes.Add(new FamilyTreeNode(person, Generation: 0));
      return id;
    }

    public void Parents(int child, params int[] parents)
    {
      foreach (var parent in parents)
        _Edges.Add(FamilyTreeEdge.ParentChild(parent, child));
    }

    // Every slot filled up to the given depth, father then mother; returns the persons by slot.
    public Dictionary<int, int> Full(int generations)
    {
      var bySlot = new Dictionary<int, int> { [1] = Person() };
      for (var slot = 2; slot < 1 << (generations + 1); slot++)
      {
        bySlot[slot] = Person(slot % 2 == 0 ? Male : Female);
        Parents(bySlot[slot / 2], bySlot[slot]);
      }
      return bySlot;
    }

    public FamilyTree Build(int centerId = 1) => new(centerId, [.. _Nodes], [.. _Edges]);
  }

  private static FanChartLayoutResult Layout(FamilyTree tree, int generations = 4, double sweep = 360) =>
    FanChartLayout.Compute(tree, generations, sweep, _metrics);

  private static Dictionary<int, int> PersonBySlot(FanChartLayoutResult layout) =>
    layout.Segments.ToDictionary(segment => segment.Slot, segment => segment.Node.Id);

  private static FanChartSegment At(FanChartLayoutResult layout, int slot) =>
    layout.Segments.Single(segment => segment.Slot == slot);

  private static bool Holds(Size canvas, Rect bounds) =>
    bounds.Left >= -Tolerance && bounds.Top >= -Tolerance &&
    bounds.Right <= canvas.Width + Tolerance && bounds.Bottom <= canvas.Height + Tolerance;

  private static Point Inside(FanChartLayoutResult layout, FanChartSegment segment)
  {
    var radius = (segment.InnerRadius + segment.OuterRadius) / 2;
    var bearing = segment.StartAngle + (segment.SweepAngle / 2);
    return FanChartLayout.PointAt(layout.Origin, radius, bearing);
  }

  [Fact]
  public void Compute_PutsTheFatherInTheEvenSlotAndTheMotherInTheOdd()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var mother = builder.Person(Female);
    var father = builder.Person(Male);
    builder.Parents(child, mother, father);

    var layout = Layout(builder.Build());

    PersonBySlot(layout).Should().Equal(new Dictionary<int, int> { [2] = father, [3] = mother });
  }

  [Fact]
  public void Compute_KeepsALoneMotherOnTheMothersSide()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var mother = builder.Person(Female);
    builder.Parents(child, mother);

    var layout = Layout(builder.Build());

    PersonBySlot(layout).Should().Equal(new Dictionary<int, int> { [3] = mother });
  }

  [Fact]
  public void Compute_PutsALoneParentOfUnknownSexInTheFathersSlot()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var parent = builder.Person(Unknown);
    builder.Parents(child, parent);

    var layout = Layout(builder.Build());

    PersonBySlot(layout).Should().Equal(new Dictionary<int, int> { [2] = parent });
  }

  [Fact]
  public void Compute_GivesAParentOfUnknownSexTheSlotTheOtherLeavesFree()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var parent = builder.Person(Unknown);
    var father = builder.Person(Male);
    builder.Parents(child, parent, father);

    var layout = Layout(builder.Build());

    PersonBySlot(layout).Should().Equal(new Dictionary<int, int> { [2] = father, [3] = parent });
  }

  [Fact]
  public void Compute_ShowsBothParentsOfASameSexCouple()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var first = builder.Person(Female);
    var second = builder.Person(Female);
    builder.Parents(child, first, second);

    var layout = Layout(builder.Build());

    PersonBySlot(layout).Should().Equal(new Dictionary<int, int> { [2] = second, [3] = first });
  }

  // Birth and adoptive parents together: two fathers and two mothers.
  [Fact]
  public void Compute_WithMoreThanTwoParentsShowsOneOfEachSex()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var father = builder.Person(Male);
    var adoptiveFather = builder.Person(Male);
    var mother = builder.Person(Female);
    var adoptiveMother = builder.Person(Female);
    builder.Parents(child, father, adoptiveFather, mother, adoptiveMother);

    var layout = Layout(builder.Build());

    PersonBySlot(layout).Should().Equal(new Dictionary<int, int> { [2] = father, [3] = mother });
  }

  [Fact]
  public void Compute_StopsAtTheRequestedGeneration()
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 5);

    var layout = Layout(builder.Build(), generations: 3);

    layout.Segments.Select(segment => segment.Slot).Should().BeEquivalentTo(Enumerable.Range(2, 14));
    layout.Segments.Max(segment => segment.Generation).Should().Be(3);
  }

  [Fact]
  public void Compute_IgnoresSpousesWhoAreNotParents()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var spouse = builder.Person(Female);
    var father = builder.Person(Male);
    builder.Parents(child, father);
    var tree = builder.Build();
    tree = tree with { Edges = [.. tree.Edges, FamilyTreeEdge.Spouse(child, spouse)] };

    var layout = Layout(tree);

    PersonBySlot(layout).Should().Equal(new Dictionary<int, int> { [2] = father });
  }

  // The parents were cousins: the father's mother and the mother's father share a father, who comes up twice.
  [Fact]
  public void Compute_DrawsACollapsedAncestorOnceAndMarksEveryLaterSlot()
  {
    var builder = new TreeBuilder();
    var center = builder.Person();
    var father = builder.Person(Male);
    var mother = builder.Person(Female);
    var paternalGrandmother = builder.Person(Female);
    var maternalGrandfather = builder.Person(Male);
    var greatGrandfather = builder.Person(Male);
    var greatGreatGrandfather = builder.Person(Male);
    builder.Parents(center, father, mother);
    builder.Parents(father, paternalGrandmother);
    builder.Parents(mother, maternalGrandfather);
    builder.Parents(paternalGrandmother, greatGrandfather);
    builder.Parents(maternalGrandfather, greatGrandfather);
    builder.Parents(greatGrandfather, greatGreatGrandfather);

    var layout = Layout(builder.Build());

    // Slot 5 is the paternal grandmother, 6 the maternal grandfather; their fathers are slots 10 and 12.
    At(layout, 10).Node.Id.Should().Be(greatGrandfather);
    At(layout, 10).IsRepeat.Should().BeFalse();
    At(layout, 12).Node.Id.Should().Be(greatGrandfather);
    At(layout, 12).IsRepeat.Should().BeTrue();
    At(layout, 20).Node.Id.Should().Be(greatGreatGrandfather);
    layout.Segments.Should().NotContain(segment => segment.Slot == 24);
  }

  // A deeper repeat still sits in its own slot's ring, not the generation it was first reached at.
  [Fact]
  public void Compute_PlacesARepeatByItsSlotsGeneration()
  {
    var builder = new TreeBuilder();
    var center = builder.Person();
    var father = builder.Person(Male);
    var grandfather = builder.Person(Male);
    builder.Parents(center, father);
    builder.Parents(father, grandfather);
    // Recorded as their own grandfather's father too, as a loop in the data would make it.
    builder.Parents(grandfather, father);

    var layout = Layout(builder.Build());

    At(layout, 8).Node.Id.Should().Be(father);
    At(layout, 8).IsRepeat.Should().BeTrue();
    At(layout, 8).Generation.Should().Be(3);
    At(layout, 8).InnerRadius.Should().Be(_metrics.CenterRadius + (2 * _metrics.RingWidth));
  }

  [Theory]
  [MemberData(nameof(Sweeps))]
  public void Compute_TilesEachRingAcrossTheSweepWithNoGapOrOverlap(double sweep)
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 4);

    var layout = Layout(builder.Build(), sweep: sweep);

    foreach (var ring in layout.Segments.GroupBy(segment => segment.Generation))
    {
      var segments = ring.OrderBy(segment => segment.Slot).ToArray();
      segments[0].StartAngle.Should().BeApproximately(-sweep / 2, Tolerance);
      segments[^1].StartAngle.Should().BeApproximately((sweep / 2) - segments[^1].SweepAngle, Tolerance);
      foreach (var (left, right) in segments.Zip(segments.Skip(1)))
        right.StartAngle.Should().BeApproximately(left.StartAngle + left.SweepAngle, Tolerance);
    }
  }

  [Fact]
  public void Compute_PutsTheFathersHalfOnTheLeftAndTheMothersOnTheRight()
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 1);

    var layout = Layout(builder.Build(), sweep: 360);

    At(layout, 2).LabelCenter.X.Should().BeLessThan(layout.Origin.X);
    At(layout, 3).LabelCenter.X.Should().BeGreaterThan(layout.Origin.X);
  }

  [Theory]
  [MemberData(nameof(Sweeps))]
  public void Compute_NeverTurnsALabelUpsideDown(double sweep)
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 7);

    var layout = Layout(builder.Build(), generations: 7, sweep: sweep);

    layout.Segments.Should().OnlyContain(segment => segment.LabelRotation > -90 && segment.LabelRotation <= 90);
  }

  // Inner rings are wider than they are deep, so the text runs along them; outer ones the other way.
  [Fact]
  public void Compute_RunsALabelAlongTheLongerSideOfItsSegment()
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 7);

    var layout = Layout(builder.Build(), generations: 7, sweep: 360);

    var top = At(layout, 4);
    top.LabelRotation.Should().BeApproximately(45, Tolerance);
    top.LabelSize.Width.Should().BeGreaterThan(top.LabelSize.Height);
    var outer = At(layout, 64);
    outer.LabelSize.Width.Should().Be(_metrics.RingWidth);
    outer.LabelSize.Height.Should().BeLessThan(_metrics.RingWidth);
  }

  [Fact]
  public void Compute_DropsTheYearsAndThenTheNameAsSegmentsThin()
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 10);

    var layout = Layout(builder.Build(), generations: 10, sweep: 360);

    foreach (var segment in layout.Segments)
    {
      var expected = segment.LabelSize.Height >= 2 * _metrics.LineHeight
        ? FanChartLabel.Full
        : segment.LabelSize.Height >= _metrics.LineHeight ? FanChartLabel.NameOnly : FanChartLabel.None;
      segment.Label.Should().Be(expected);
    }
    layout.Segments.Select(segment => segment.Label).Distinct().Should().BeEquivalentTo(new[] { FanChartLabel.Full, FanChartLabel.NameOnly, FanChartLabel.None });
  }

  [Theory]
  [MemberData(nameof(Sweeps))]
  public void Compute_SizesTheCanvasToHoldEverySegmentAndTheCentre(double sweep)
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 4);

    var layout = Layout(builder.Build(), sweep: sweep);

    var disc = FanChartLayout.SectorBounds(layout.Origin, 0, _metrics.CenterRadius, -180, 360);
    Holds(layout.CanvasSize, disc).Should().BeTrue();
    foreach (var segment in layout.Segments)
    {
      var bounds = FanChartLayout.SectorBounds(layout.Origin, segment.InnerRadius, segment.OuterRadius, segment.StartAngle, segment.SweepAngle);
      Holds(layout.CanvasSize, bounds).Should().BeTrue($"slot {segment.Slot}");
    }
  }

  [Fact]
  public void Compute_MakesAFullCircleSquareAndAHalfOneTwiceAsWideAsTheFanIsTall()
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 4);
    var tree = builder.Build();
    var radius = _metrics.CenterRadius + (4 * _metrics.RingWidth);
    var margins = 2 * _metrics.Margin;

    var circle = Layout(tree, sweep: 360);
    var half = Layout(tree, sweep: 180);

    circle.CanvasSize.Width.Should().BeApproximately((2 * radius) + margins, Tolerance);
    circle.CanvasSize.Height.Should().BeApproximately((2 * radius) + margins, Tolerance);
    half.CanvasSize.Width.Should().BeApproximately((2 * radius) + margins, Tolerance);
    half.CanvasSize.Height.Should().BeApproximately(radius + _metrics.CenterRadius + margins, Tolerance);
  }

  [Fact]
  public void Compute_WithNoAncestorsLeavesJustTheCentre()
  {
    var builder = new TreeBuilder();
    var center = builder.Person();

    var layout = Layout(builder.Build());

    layout.Center.Id.Should().Be(center);
    layout.Segments.Should().BeEmpty();
    layout.CanvasSize.Width.Should().BeApproximately((2 * _metrics.CenterRadius) + (2 * _metrics.Margin), Tolerance);
  }

  [Theory]
  [MemberData(nameof(Sweeps))]
  public void HitTest_FindsThePersonInEverySegment(double sweep)
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 5);

    var layout = Layout(builder.Build(), generations: 5, sweep: sweep);

    foreach (var segment in layout.Segments)
    {
      var point = Inside(layout, segment);
      FanChartLayout.HitTest(layout, point).Should().Be(segment.Node, $"slot {segment.Slot}");
    }
  }

  [Fact]
  public void HitTest_FindsTheCentreInsideTheDisc()
  {
    var builder = new TreeBuilder();
    builder.Full(generations: 2);
    var layout = Layout(builder.Build());

    var point = new Point(layout.Origin.X + (_metrics.CenterRadius / 2), layout.Origin.Y);

    FanChartLayout.HitTest(layout, point).Should().Be(layout.Center);
  }

  [Fact]
  public void HitTest_FindsNobodyWhereNoSegmentIsDrawn()
  {
    var builder = new TreeBuilder();
    var child = builder.Person();
    var father = builder.Person(Male);
    builder.Parents(child, father);
    var layout = Layout(builder.Build(), sweep: 180);

    var mothersSlot = FanChartLayout.PointAt(layout.Origin, _metrics.CenterRadius + (_metrics.RingWidth / 2), 45);
    var belowTheFan = FanChartLayout.PointAt(layout.Origin, _metrics.CenterRadius + (_metrics.RingWidth / 2), 180);
    var pastTheOuterRing = FanChartLayout.PointAt(layout.Origin, _metrics.CenterRadius + (1.5 * _metrics.RingWidth), -45);

    FanChartLayout.HitTest(layout, mothersSlot).Should().BeNull();
    FanChartLayout.HitTest(layout, belowTheFan).Should().BeNull();
    FanChartLayout.HitTest(layout, pastTheOuterRing).Should().BeNull();
  }
}
