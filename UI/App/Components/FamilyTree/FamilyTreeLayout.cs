using GT4.Core.Project.Dto;

namespace GT4.UI.Components.Genealogy;

/// <summary>Sizing knobs for <see cref="FamilyTreeLayout"/>, all in device-independent units.</summary>
public sealed record FamilyTreeLayoutMetrics(
  double NodeWidth = 124,
  double NodeHeight = 104,
  double HorizontalGap = 28,
  double VerticalGap = 72,
  double Margin = 32,
  double CornerRadius = 14)
{
  public double SlotPitch => NodeWidth + HorizontalGap;
  public double RowPitch => NodeHeight + VerticalGap;
}

/// <summary>A node together with the rectangle it occupies on the canvas.</summary>
public sealed record FamilyTreeNodeLayout(FamilyTreeNode Node, Rect Bounds);

/// <summary>A poly-line connector between two nodes, ready to be stroked with rounded bends.</summary>
public sealed record FamilyTreeConnector(FamilyTreeRelation Relation, PointF[] Points);

public sealed record FamilyTreeLayoutResult(
  IReadOnlyList<FamilyTreeNodeLayout> Nodes,
  IReadOnlyList<FamilyTreeConnector> Connectors,
  Size CanvasSize,
  Point CenterTopLeft);

/// <summary>
/// Turns a <see cref="FamilyTree"/> into absolute node rectangles and orthogonal connectors.
/// <para>
/// A tidy-tree DFS seeds every node with a starting column. Previously laid-out nodes then override
/// that seed with the X positions they held in the last render, so loading a new generation does not
/// displace nodes already on screen. New nodes are pre-seeded at the weighted average of their
/// already-placed neighbours, then a spring simulation relaxes all X positions simultaneously: edges
/// attract connected nodes toward each other, same-row nodes repel within a short radius, and
/// already-placed nodes weakly resist drifting from their prior column. A final sweep enforces the
/// minimum one-slot gap within every row.
/// </para>
/// </summary>
public sealed class FamilyTreeLayout
{
  // X slot positions from the last render, keyed by node ID.
  private Dictionary<int, double> _xSlots = [];

  // Spring simulation tuning constants. All forces act in "slot" units (1 slot = NodeWidth + HGap).
  private const double SpringK = 0.25;      // attraction along each edge per slot of separation
  private const double RepelK = 0.60;       // same-row repulsion magnitude
  private const double RepelRadius = 2.5;   // repulsion cuts off beyond this many slots
  private const double AnchorK = 1.80;      // resistance of previously placed nodes to drift
  private const double Damping = 0.80;      // per-step velocity decay (overdamped → no oscillation)
  private const double TimeStep = 0.50;
  private const int Iterations = 200;
  private const double ParentChildWeight = 1.0;
  private const double SpouseWeight = 0.5;

  /// <summary>
  /// Clears stored positions so the next <see cref="Update"/> starts from scratch.
  /// Call this when the centred person changes.
  /// </summary>
  public void Reset() => _xSlots = [];

  public FamilyTreeLayoutResult Update(FamilyTree tree, FamilyTreeLayoutMetrics metrics)
  {
    ArgumentNullException.ThrowIfNull(tree);
    ArgumentNullException.ThrowIfNull(metrics);

    if (tree.Nodes.Count == 0)
      return new FamilyTreeLayoutResult([], [], new Size(0, 0), new Point(0, 0));

    var nodesById = tree.Nodes.ToDictionary(n => n.Id);
    var childrenByParent = BuildAdjacency(tree, nodesById, descendants: true);
    var parentsByChild = BuildAdjacency(tree, nodesById, descendants: false);

    // Tidy-tree seeding: gives every node a structurally reasonable column to start from.
    var x = new Dictionary<int, double>();
    LayoutBranch(tree.CenterId, childrenByParent, x);
    AlignBranch(tree.CenterId, parentsByChild, x);
    PlaceCollaterals(nodesById, parentsByChild, x);
    PlaceSpouses(tree, nodesById, x);
    FillUnplaced(nodesById, x);

    // Existing nodes override the tidy-tree seed with their stored columns so they don't jump when
    // a new generation is added above or below.
    var existing = new HashSet<int>();
    foreach (var (id, storedX) in _xSlots)
    {
      if (!x.ContainsKey(id))
        continue;
      x[id] = storedX;
      existing.Add(id);
    }

    // New nodes: pre-seed at the weighted average of already-placed neighbours so the spring only
    // needs fine-tuning rather than covering a large initial gap.
    ReseedNewNodes(tree, x, existing);

    SpringRelax(tree, nodesById, x, existing);
    ResolveRowOverlaps(nodesById, x);

    _xSlots = new Dictionary<int, double>(x);
    return Assemble(tree, nodesById, x, metrics);
  }

  // Walks the graph from new nodes outward, pulling each one toward the weighted-average X of its
  // already-seeded neighbours.  Multiple passes handle chains of consecutive new nodes.
  private static void ReseedNewNodes(
    FamilyTree tree,
    Dictionary<int, double> x,
    HashSet<int> existing)
  {
    var adj = x.Keys.ToDictionary(id => id, _ => new List<(int Id, double W)>());
    foreach (var edge in tree.Edges)
    {
      if (!x.ContainsKey(edge.FromId) || !x.ContainsKey(edge.ToId))
        continue;
      var w = edge.Relation == FamilyTreeRelation.Spouse ? SpouseWeight : ParentChildWeight;
      adj[edge.FromId].Add((edge.ToId, w));
      adj[edge.ToId].Add((edge.FromId, w));
    }

    var seeded = new HashSet<int>(existing);
    var changed = true;
    while (changed)
    {
      changed = false;
      foreach (var id in x.Keys)
      {
        if (seeded.Contains(id))
          continue;
        var placed = adj[id].Where(n => seeded.Contains(n.Id)).ToList();
        if (placed.Count == 0)
          continue;
        var totalW = placed.Sum(n => n.W);
        x[id] = placed.Sum(n => x[n.Id] * n.W) / totalW;
        seeded.Add(id);
        changed = true;
      }
    }
  }

  private static void SpringRelax(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    HashSet<int> existing)
  {
    var velocity = nodesById.Keys.ToDictionary(id => id, _ => 0.0);

    // Snapshot existing positions for the anchor force.
    var anchorX = existing.ToDictionary(id => id, id => x[id]);

    // Group node IDs by generation: only same-row pairs repel each other.
    var rows = nodesById.Values
      .GroupBy(n => n.Generation)
      .ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToList());

    for (var iter = 0; iter < Iterations; iter++)
    {
      var forces = nodesById.Keys.ToDictionary(id => id, _ => 0.0);

      // Edge springs: pull each connected pair toward the same horizontal position.
      foreach (var edge in tree.Edges)
      {
        if (!x.ContainsKey(edge.FromId) || !x.ContainsKey(edge.ToId))
          continue;
        var w = edge.Relation == FamilyTreeRelation.Spouse ? SpouseWeight : ParentChildWeight;
        var dx = x[edge.ToId] - x[edge.FromId];
        var f = SpringK * dx * w;
        forces[edge.FromId] += f;
        forces[edge.ToId] -= f;
      }

      // Same-row repulsion: linear force that drops to zero at RepelRadius slots.
      foreach (var row in rows.Values)
      {
        for (var i = 0; i < row.Count; i++)
        {
          for (var j = i + 1; j < row.Count; j++)
          {
            var dx = x[row[j]] - x[row[i]];
            var dist = Math.Abs(dx);
            if (dist >= RepelRadius)
              continue;
            var f = RepelK * (RepelRadius - dist) / RepelRadius;
            var sign = dx >= 0 ? 1.0 : -1.0;
            forces[row[i]] -= f * sign;
            forces[row[j]] += f * sign;
          }
        }
      }

      // Anchor: previously placed nodes resist drifting from where they were last render.
      foreach (var (id, ax) in anchorX)
        forces[id] += AnchorK * (ax - x[id]);

      // Semi-implicit Euler integration with velocity damping.
      foreach (var id in nodesById.Keys)
      {
        velocity[id] = (velocity[id] + forces[id] * TimeStep) * Damping;
        x[id] += velocity[id] * TimeStep;
      }
    }
  }

  // ── helpers carried over from the original static implementation ─────────────────────────────

  private static Dictionary<int, List<int>> BuildAdjacency(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    bool descendants)
  {
    var adjacency = new Dictionary<int, List<int>>();
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.ParentChild)
        continue;
      var (key, value) = descendants ? (edge.FromId, edge.ToId) : (edge.ToId, edge.FromId);
      if (!nodesById.ContainsKey(key) || !nodesById.ContainsKey(value))
        continue;
      if (!adjacency.TryGetValue(key, out var list))
        adjacency[key] = list = [];
      list.Add(value);
    }
    return adjacency;
  }

  private static void LayoutBranch(int rootId, Dictionary<int, List<int>> childrenOf, Dictionary<int, double> x)
  {
    var cursor = 0.0;
    AssignSlots(rootId, childrenOf, x, [], ref cursor);
  }

  private static double AssignSlots(
    int id,
    Dictionary<int, List<int>> childrenOf,
    Dictionary<int, double> x,
    HashSet<int> visited,
    ref double cursor)
  {
    visited.Add(id);
    var children = childrenOf.TryGetValue(id, out var list)
      ? list.Where(c => !visited.Contains(c)).ToList()
      : [];

    if (children.Count == 0)
    {
      x[id] = cursor++;
      return x[id];
    }

    var sum = 0.0;
    foreach (var child in children)
      sum += AssignSlots(child, childrenOf, x, visited, ref cursor);

    x[id] = sum / children.Count;
    return x[id];
  }

  private static void AlignBranch(int rootId, Dictionary<int, List<int>> parentsOf, Dictionary<int, double> x)
  {
    var branchX = new Dictionary<int, double>();
    var cursor = 0.0;
    AssignSlots(rootId, parentsOf, branchX, [], ref cursor);
    var shift = x[rootId] - branchX[rootId];
    foreach (var (id, slot) in branchX)
    {
      if (id != rootId)
        x[id] = slot + shift;
    }
  }

  private static void PlaceCollaterals(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, List<int>> parentsByChild,
    Dictionary<int, double> x)
  {
    foreach (var node in nodesById.Values.OrderByDescending(n => n.Generation))
    {
      if (x.ContainsKey(node.Id))
        continue;
      if (!parentsByChild.TryGetValue(node.Id, out var parents))
        continue;
      var placed = parents.Where(x.ContainsKey).Select(p => x[p]).ToList();
      if (placed.Count != 0)
        x[node.Id] = placed.Average();
    }
  }

  private static void PlaceSpouses(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x)
  {
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.Spouse)
        continue;
      var aPlaced = x.ContainsKey(edge.FromId);
      var bPlaced = x.ContainsKey(edge.ToId);
      if (aPlaced && !bPlaced && nodesById.ContainsKey(edge.ToId))
        x[edge.ToId] = x[edge.FromId] + 1;
      else if (bPlaced && !aPlaced && nodesById.ContainsKey(edge.FromId))
        x[edge.FromId] = x[edge.ToId] + 1;
    }
  }

  private static void FillUnplaced(IReadOnlyDictionary<int, FamilyTreeNode> nodesById, Dictionary<int, double> x)
  {
    foreach (var node in nodesById.Values)
      x.TryAdd(node.Id, 0);
  }

  private static void ResolveRowOverlaps(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x)
  {
    foreach (var row in nodesById.Values.GroupBy(n => n.Generation))
    {
      var ordered = row.OrderBy(n => x[n.Id]).ThenBy(n => n.Id).ToList();
      for (var i = 1; i < ordered.Count; i++)
      {
        var minimum = x[ordered[i - 1].Id] + 1;
        if (x[ordered[i].Id] < minimum)
          x[ordered[i].Id] = minimum;
      }
    }
  }

  private static FamilyTreeLayoutResult Assemble(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    FamilyTreeLayoutMetrics metrics)
  {
    var maxGeneration = nodesById.Values.Max(n => n.Generation);
    var minSlot = x.Values.Min();

    double Left(int id) => metrics.Margin + (x[id] - minSlot) * metrics.SlotPitch;
    double Top(int generation) => metrics.Margin + (maxGeneration - generation) * metrics.RowPitch;

    var layouts = new List<FamilyTreeNodeLayout>(nodesById.Count);
    var bounds = new Dictionary<int, Rect>(nodesById.Count);

    foreach (var node in nodesById.Values)
    {
      var rect = new Rect(Left(node.Id), Top(node.Generation), metrics.NodeWidth, metrics.NodeHeight);
      bounds[node.Id] = rect;
      layouts.Add(new FamilyTreeNodeLayout(node, rect));
    }

    var connectors = BuildConnectors(tree, bounds, metrics);
    var width = layouts.Max(l => l.Bounds.Right) + metrics.Margin;
    var height = layouts.Max(l => l.Bounds.Bottom) + metrics.Margin;
    var centerTopLeft = bounds.TryGetValue(tree.CenterId, out var centerRect)
      ? centerRect.Location
      : new Point(0, 0);

    return new FamilyTreeLayoutResult(layouts, connectors, new Size(width, height), centerTopLeft);
  }

  private static List<FamilyTreeConnector> BuildConnectors(
    FamilyTree tree,
    IReadOnlyDictionary<int, Rect> bounds,
    FamilyTreeLayoutMetrics metrics)
  {
    var connectors = new List<FamilyTreeConnector>(tree.Edges.Count);
    foreach (var edge in tree.Edges)
    {
      if (!bounds.TryGetValue(edge.FromId, out var from) || !bounds.TryGetValue(edge.ToId, out var to))
        continue;
      var points = edge.Relation == FamilyTreeRelation.ParentChild
        ? ParentChildPath(from, to)
        : SpousePath(from, to);
      connectors.Add(new FamilyTreeConnector(edge.Relation, points));
    }
    return connectors;
  }

  private static PointF[] ParentChildPath(Rect parent, Rect child)
  {
    var startX = (float)parent.Center.X;
    var startY = (float)parent.Bottom;
    var endX = (float)child.Center.X;
    var endY = (float)child.Top;
    var midY = (startY + endY) / 2f;
    return
    [
      new PointF(startX, startY),
      new PointF(startX, midY),
      new PointF(endX, midY),
      new PointF(endX, endY),
    ];
  }

  private static PointF[] SpousePath(Rect a, Rect b)
  {
    var (left, right) = a.Center.X <= b.Center.X ? (a, b) : (b, a);
    var y = (float)left.Center.Y;
    return
    [
      new PointF((float)left.Right, y),
      new PointF((float)right.Left, y),
    ];
  }
}
