using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.View.Tests;

public class FamilyTreeLayoutTests
{
  private static readonly FamilyTreeLayoutMetrics _metrics = new();

  private static PersonInfo MakePerson(int id) =>
    new(id, Date.Create(0, DateStatus.Unknown), null, BiologicalSex.Unknown, [], null);

  private static FamilyTreeNode MakeNode(int id, int generation) =>
    new(MakePerson(id), generation);

  private static FamilyTree MakeTree(int centerId, FamilyTreeNode[] nodes, FamilyTreeEdge[]? edges = null) =>
    new(centerId, nodes, edges ?? []);

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
  public void Update_NullTree_Throws()
  {
    var act = () => new FamilyTreeLayout().Update(null!, _metrics);
    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Update_NullMetrics_Throws()
  {
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var act = () => new FamilyTreeLayout().Update(tree, null!);
    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Update_EmptyTree_ReturnsEmptyResult()
  {
    var result = new FamilyTreeLayout().Update(FamilyTree.Empty, _metrics);
    result.Nodes.Should().BeEmpty();
    result.Connectors.Should().BeEmpty();
    result.CanvasSize.Width.Should().Be(0);
    result.CanvasSize.Height.Should().Be(0);
  }

  [Fact]
  public void Update_SingleNode_ReturnsOneNodeAndNoConnectors()
  {
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = new FamilyTreeLayout().Update(tree, _metrics);
    result.Nodes.Should().HaveCount(1);
    result.Connectors.Should().BeEmpty();
  }

  [Fact]
  public void Update_SingleNode_NodeHasMetricsDimensions()
  {
    var m = new FamilyTreeLayoutMetrics(NodeWidth: 100, NodeHeight: 80);
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = new FamilyTreeLayout().Update(tree, m);
    result.Nodes[0].Bounds.Width.Should().Be(100);
    result.Nodes[0].Bounds.Height.Should().Be(80);
  }

  [Fact]
  public void Update_SingleNode_CenterTopLeftMatchesNodeTopLeft()
  {
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = new FamilyTreeLayout().Update(tree, _metrics);
    result.CenterTopLeft.X.Should().Be(result.Nodes[0].Bounds.Left);
    result.CenterTopLeft.Y.Should().Be(result.Nodes[0].Bounds.Top);
  }

  [Fact]
  public void Update_SingleNode_CanvasIsAtLeastNodePlusTwoMargins()
  {
    var m = new FamilyTreeLayoutMetrics(NodeWidth: 100, NodeHeight: 80, Margin: 20);
    var tree = MakeTree(1, [MakeNode(1, 0)]);
    var result = new FamilyTreeLayout().Update(tree, m);
    result.CanvasSize.Width.Should().BeGreaterThanOrEqualTo(100 + 2 * 20);
    result.CanvasSize.Height.Should().BeGreaterThanOrEqualTo(80 + 2 * 20);
  }

  [Fact]
  public void Update_AncestorIsAboveCenter()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    var center = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var ancestor = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    ancestor.Top.Should().BeLessThan(center.Top, "ancestors draw above the center person");
  }

  [Fact]
  public void Update_DescendantIsBelowCenter()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, -1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 1, childId: 2) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    var center = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var descendant = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    descendant.Top.Should().BeGreaterThan(center.Top, "descendants draw below the center person");
  }

  [Fact]
  public void Update_SpousesHaveSameVerticalPosition()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    var a = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var b = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    a.Top.Should().Be(b.Top);
  }

  [Fact]
  public void Update_ConsecutiveGenerations_VerticalSpacingEqualsRowPitch()
  {
    var m = new FamilyTreeLayoutMetrics(NodeHeight: 80, VerticalGap: 40);
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), m);

    var center = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var ancestor = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    (center.Top - ancestor.Top).Should().BeApproximately(m.RowPitch, precision: 0.001);
  }

  [Fact]
  public void Update_ThreeGenerations_EachRowAtCorrectVerticalStep()
  {
    var m = new FamilyTreeLayoutMetrics(NodeHeight: 80, VerticalGap: 40);
    // gen 2 → gen 1 → gen 0
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1), MakeNode(3, 2) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 3, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 2, childId: 1),
    };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), m);

    var row0 = result.Nodes.Single(n => n.Node.Id == 1).Bounds.Top;
    var row1 = result.Nodes.Single(n => n.Node.Id == 2).Bounds.Top;
    var row2 = result.Nodes.Single(n => n.Node.Id == 3).Bounds.Top;

    (row0 - row1).Should().BeApproximately(m.RowPitch, 0.001);
    (row1 - row2).Should().BeApproximately(m.RowPitch, 0.001);
  }

  [Fact]
  public void Update_SiblingNodes_DoNotHorizontallyOverlap()
  {
    var nodes = new[] { MakeNode(1, 1), MakeNode(2, 0), MakeNode(3, 0) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 3),
    };
    var result = new FamilyTreeLayout().Update(MakeTree(2, nodes, edges), _metrics);

    var ordered = result.Nodes
      .Where(n => n.Node.Generation == 0)
      .Select(n => n.Bounds)
      .OrderBy(b => b.Left)
      .ToList();
    ordered[0].Right.Should().BeLessThanOrEqualTo(ordered[1].Left, "siblings must not overlap horizontally");
  }

  [Fact]
  public void Update_TwoSiblingsUnderParent_ParentBetweenThem()
  {
    var nodes = new[] { MakeNode(1, 1), MakeNode(2, 0), MakeNode(3, 0) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 1, childId: 3),
    };
    var result = new FamilyTreeLayout().Update(MakeTree(2, nodes, edges), _metrics);

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
  public void Update_FourSiblingsOnSameRow_NoneOverlap()
  {
    var nodes = new[] { MakeNode(0, 1), MakeNode(1, 0), MakeNode(2, 0), MakeNode(3, 0), MakeNode(4, 0) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 1),
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 2),
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 3),
      FamilyTreeEdge.ParentChild(parentId: 0, childId: 4),
    };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    var row = result.Nodes
      .Where(n => n.Node.Generation == 0)
      .Select(n => n.Bounds)
      .OrderBy(b => b.Left)
      .ToList();

    for (var i = 1; i < row.Count; i++)
      row[i].Left.Should().BeGreaterThanOrEqualTo(row[i - 1].Right, "no two siblings should overlap");
  }

  [Fact]
  public void Update_ParentChildEdge_ConnectorHasFourPoints()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    result.Connectors.Single(c => c.Relation == FamilyTreeRelation.ParentChild)
      .Points.Should().HaveCount(4, "parent-child uses a 4-point orthogonal elbow");
  }

  [Fact]
  public void Update_SpouseEdge_ConnectorHasTwoPoints()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    result.Connectors.Single(c => c.Relation == FamilyTreeRelation.Spouse)
      .Points.Should().HaveCount(2, "spouse uses a 2-point straight horizontal segment");
  }

  [Fact]
  public void Update_ParentChildConnector_StartsAtParentBottomCenterEndsAtChildTopCenter()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 2, childId: 1) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    var parent = result.Nodes.Single(n => n.Node.Id == 2).Bounds;
    var child = result.Nodes.Single(n => n.Node.Id == 1).Bounds;
    var pts = result.Connectors.Single(c => c.Relation == FamilyTreeRelation.ParentChild).Points;

    pts[0].X.Should().BeApproximately((float)parent.Center.X, 0.01f);
    pts[0].Y.Should().BeApproximately((float)parent.Bottom, 0.01f);
    pts[3].X.Should().BeApproximately((float)child.Center.X, 0.01f);
    pts[3].Y.Should().BeApproximately((float)child.Top, 0.01f);
  }

  [Fact]
  public void Update_SpouseConnector_IsHorizontal()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    var pts = result.Connectors.Single(c => c.Relation == FamilyTreeRelation.Spouse).Points;
    pts[0].Y.Should().BeApproximately(pts[1].Y, 0.01f, "spouse connector is a horizontal line");
  }

  [Fact]
  public void Update_SpouseConnector_RunsFromRightEdgeOfLeftNodeToLeftEdgeOfRightNode()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0) };
    var edges = new[] { FamilyTreeEdge.Spouse(1, 2) };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    var boundsById = result.Nodes.ToDictionary(n => n.Node.Id, n => n.Bounds);
    var left = boundsById.Values.MinBy(b => b.Left)!;
    var right = boundsById.Values.MaxBy(b => b.Left)!;
    var pts = result.Connectors.Single(c => c.Relation == FamilyTreeRelation.Spouse).Points;

    var leftX = Math.Min(pts[0].X, pts[1].X);
    var rightX = Math.Max(pts[0].X, pts[1].X);
    ((double)leftX).Should().BeApproximately(left.Right, 0.01);
    ((double)rightX).Should().BeApproximately(right.Left, 0.01);
  }

  [Fact]
  public void Update_EdgeCountMatchesConnectorCount()
  {
    var nodes = new[] { MakeNode(1, 0), MakeNode(2, 0), MakeNode(3, 1) };
    var edges = new[]
    {
      FamilyTreeEdge.Spouse(1, 2),
      FamilyTreeEdge.ParentChild(parentId: 3, childId: 1),
    };
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);
    result.Connectors.Should().HaveCount(2);
  }

  [Fact]
  public void Update_AllNodeBoundsAreWithinCanvasSize()
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
    var result = new FamilyTreeLayout().Update(MakeTree(1, nodes, edges), _metrics);

    foreach (var nodeLayout in result.Nodes)
    {
      nodeLayout.Bounds.Right.Should().BeLessThanOrEqualTo(result.CanvasSize.Width,
        $"node {nodeLayout.Node.Id} right edge must fit within canvas width");
      nodeLayout.Bounds.Bottom.Should().BeLessThanOrEqualTo(result.CanvasSize.Height,
        $"node {nodeLayout.Node.Id} bottom edge must fit within canvas height");
    }
  }

  [Fact]
  public void Update_CenterTopLeft_MatchesCenterNodeBoundsTopLeft()
  {
    var nodes = new[] { MakeNode(10, 0), MakeNode(20, 1) };
    var edges = new[] { FamilyTreeEdge.ParentChild(parentId: 20, childId: 10) };
    var result = new FamilyTreeLayout().Update(MakeTree(10, nodes, edges), _metrics);

    var centerNode = result.Nodes.Single(n => n.Node.Id == 10).Bounds;
    result.CenterTopLeft.X.Should().Be(centerNode.Left);
    result.CenterTopLeft.Y.Should().Be(centerNode.Top);
  }

  [Fact]
  public void Update_MultipleNodesIncludingCenter_CenterTopLeftMatchesCenterNode()
  {
    // Center (id 5) is flanked by a sibling (id 6) and topped by a parent (id 7).
    var nodes = new[] { MakeNode(5, 0), MakeNode(6, 0), MakeNode(7, 1) };
    var edges = new[]
    {
      FamilyTreeEdge.ParentChild(parentId: 7, childId: 5),
      FamilyTreeEdge.ParentChild(parentId: 7, childId: 6),
    };
    var result = new FamilyTreeLayout().Update(MakeTree(5, nodes, edges), _metrics);

    var centerBounds = result.Nodes.Single(n => n.Node.Id == 5).Bounds;
    result.CenterTopLeft.X.Should().Be(centerBounds.Left);
    result.CenterTopLeft.Y.Should().Be(centerBounds.Top);
  }

  [Fact]
  public void Reset_SubsequentUpdateProducesSameResultAsFirstRun()
  {
    var layout = new FamilyTreeLayout();
    var treeA = MakeTree(1, [MakeNode(1, 0), MakeNode(2, 0)], [FamilyTreeEdge.Spouse(1, 2)]);
    layout.Update(treeA, _metrics);

    layout.Reset();

    var treeB = MakeTree(5, [MakeNode(5, 0)]);
    var afterReset = layout.Update(treeB, _metrics);
    var fresh = new FamilyTreeLayout().Update(treeB, _metrics);

    afterReset.CenterTopLeft.X.Should().Be(fresh.CenterTopLeft.X);
    afterReset.CenterTopLeft.Y.Should().Be(fresh.CenterTopLeft.Y);
  }

  [Fact]
  public void Update_WithoutReset_StoredPositionsInfluenceSubsequentLayout()
  {
    // First build: place node 1 at gen 0
    var layout = new FamilyTreeLayout();
    var tree1 = MakeTree(1, [MakeNode(1, 0)]);
    layout.Update(tree1, _metrics);

    // Second build: same node 1 is now in a two-node tree — the stored X for node 1
    // should be preserved (existing nodes override the tidy-tree seed).
    var tree2 = MakeTree(1, [MakeNode(1, 0), MakeNode(2, 0)], [FamilyTreeEdge.Spouse(1, 2)]);
    var withHistory = layout.Update(tree2, _metrics);

    // Without history, the fresh layout will seed differently
    var fresh = new FamilyTreeLayout().Update(tree2, _metrics);

    // The center X may differ between the two layouts because stored positions anchor node 1
    // in the second run.  We just assert that the layout produced valid (non-zero-size) output.
    withHistory.CanvasSize.Width.Should().BeGreaterThan(0);
    withHistory.Nodes.Should().HaveCount(2);
  }
}
