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
/// A tidy-tree DFS seeds every node with a starting column. Previously laid-out nodes override
/// that seed with their last-render positions so loading a new generation does not displace nodes
/// already on screen. New nodes are pre-seeded at the weighted average of their already-placed
/// neighbours.
/// </para>
/// <para>
/// Two optimisation passes then run in sequence. First, <em>swap refinement</em> tests every
/// adjacent pair within each generation: if exchanging the two X positions reduces the combined
/// spring + anchor + overlap energy, the swap is kept and the pass repeats until no pair improves.
/// The overlap term charges a heavy fee whenever two horizontal connector runs share a generation
/// band — every parent-child edge crossing the same pair of generations draws its mid-height run at
/// one shared Y, so collinear runs are penalised hard to keep them from stacking on top of each
/// other. This lets nodes find the globally optimal left-to-right ordering within their row —
/// something the spring simulation cannot discover because repulsion prevents nodes from crossing.
/// Second, the
/// <em>spring simulation</em> fine-tunes X positions within the settled ordering: edges attract,
/// same-row nodes repel, and already-placed nodes weakly resist positional drift. A final sweep
/// enforces the minimum one-slot gap.
/// </para>
/// </summary>
public sealed class FamilyTreeLayout
{
  private Dictionary<int, double> _xSlots = [];

  // All force constants act in slot units (1 slot = NodeWidth + HorizontalGap).
  private const double SpringK = 0.25;      // attraction per slot of edge separation
  private const double RepelK = 0.60;       // same-row repulsion magnitude
  private const double RepelRadius = 2.5;   // repulsion cuts off beyond this many slots
  private const double AnchorK = 0.80;      // resistance of previously placed nodes to drift
  private const double Damping = 0.80;
  private const double TimeStep = 0.50;
  private const int Iterations = 200;
  private const double ParentChildWeight = 1.0;
  private const double SpouseWeight = 0.5;
  // Fee per slot of overlap between two horizontal connector runs sharing a generation band.
  // Kept far larger than any spring/anchor term so eliminating overlap dominates every swap.
  private const double OverlapK = 100.0;

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

    // Tidy-tree seeding gives every node a structurally reasonable starting column.
    var x = new Dictionary<int, double>();
    LayoutBranch(tree.CenterId, childrenByParent, x);
    AlignBranch(tree.CenterId, parentsByChild, x);
    PlaceCollaterals(nodesById, parentsByChild, x);
    PlaceSpouses(tree, nodesById, x);
    FillUnplaced(nodesById, x);

    // Existing nodes override the tidy-tree seed with their stored columns.
    var existing = new HashSet<int>();
    foreach (var (id, storedX) in _xSlots)
    {
      if (!x.ContainsKey(id))
        continue;
      x[id] = storedX;
      existing.Add(id);
    }

    // Build weighted adjacency once; shared by reseed, swap, and spring passes.
    var adj = BuildEdgeAdjacency(tree, nodesById);

    // Horizontal connector runs grouped by the generation band they share, used by the overlap fee.
    var (segByNode, segByLevel) = BuildHorizontalSegments(tree, nodesById);

    // Pre-seed new nodes toward their already-placed neighbours.
    ReseedNewNodes(x, existing, adj);

    // ── ordering phase ────────────────────────────────────────────────────────────────────────
    // Anchor to original stored positions when evaluating swap energy: a swap for an existing node
    // is only kept if the spring savings outweigh the cost of moving away from its prior slot.
    var originalAnchorX = existing.ToDictionary(id => id, id => x[id]);
    SwapRefinement(nodesById, x, adj, originalAnchorX, segByNode, segByLevel);

    // ── position phase ────────────────────────────────────────────────────────────────────────
    // Anchor to post-swap positions so the spring refines within the new ordering rather than
    // pulling nodes back to where they were before the swap.
    var springAnchorX = existing.ToDictionary(id => id, id => x[id]);
    SpringRelax(nodesById, x, adj, springAnchorX);

    ResolveRowOverlaps(nodesById, x);

    _xSlots = new Dictionary<int, double>(x);
    return Assemble(tree, nodesById, x, metrics);
  }

  // ── optimisation passes ───────────────────────────────────────────────────────────────────────

  // Builds a symmetric, weighted adjacency list for every node in the tree.
  private static Dictionary<int, List<(int Id, double W)>> BuildEdgeAdjacency(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById)
  {
    var adj = nodesById.Keys.ToDictionary(id => id, _ => new List<(int Id, double W)>());
    foreach (var edge in tree.Edges)
    {
      if (!adj.ContainsKey(edge.FromId) || !adj.ContainsKey(edge.ToId))
        continue;
      var w = edge.Relation == FamilyTreeRelation.Spouse ? SpouseWeight : ParentChildWeight;
      adj[edge.FromId].Add((edge.ToId, w));
      adj[edge.ToId].Add((edge.FromId, w));
    }
    return adj;
  }

  // Pre-seed new nodes at the weighted average X of already-placed neighbours so the spring covers
  // a much smaller initial gap.  Chains of new nodes are handled by running multiple passes.
  private static void ReseedNewNodes(
    Dictionary<int, double> x,
    HashSet<int> existing,
    IReadOnlyDictionary<int, List<(int Id, double W)>> adj)
  {
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

  // Tests every adjacent pair within each generation and swaps their X positions whenever doing so
  // reduces the combined spring + anchor + overlap energy.  The process repeats until no pair
  // improves, converging on the energy-minimising permutation within each row.
  //
  // The spring/anchor terms use NodeEnergy, where the A–B edge (if one exists) is excluded because
  // swapping leaves the two nodes equally far apart — its contribution cancels on both sides. The
  // overlap term charges OverlapK per slot that two horizontal connector runs share a generation
  // band; because OverlapK dwarfs the spring terms, the pass treats removing overlap as paramount.
  private static void SwapRefinement(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    IReadOnlyDictionary<int, List<(int Id, double W)>> adj,
    IReadOnlyDictionary<int, double> anchorX,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByNode,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByLevel)
  {
    var rows = nodesById.Values
      .GroupBy(n => n.Generation)
      .ToDictionary(g => g.Key, g => g.Select(n => n.Id).OrderBy(id => x[id]).ToList());

    var improved = true;
    while (improved)
    {
      improved = false;
      foreach (var row in rows.Values)
      {
        if (row.Count < 2)
          continue;
        row.Sort((a, b) => x[a].CompareTo(x[b]));

        for (var i = 0; i < row.Count - 1; i++)
        {
          var idA = row[i];
          var idB = row[i + 1];
          var xA = x[idA];
          var xB = x[idB];

          var beforeE = NodeEnergy(idA, xA, adj[idA], idB, x, anchorX)
                      + NodeEnergy(idB, xB, adj[idB], idA, x, anchorX)
                      + OverlapFee(idA, idB, segByNode, segByLevel, x);

          x[idA] = xB;
          x[idB] = xA;

          var afterE = NodeEnergy(idA, xB, adj[idA], idB, x, anchorX)
                     + NodeEnergy(idB, xA, adj[idB], idA, x, anchorX)
                     + OverlapFee(idA, idB, segByNode, segByLevel, x);

          if (afterE < beforeE - 1e-9)
          {
            (row[i], row[i + 1]) = (row[i + 1], row[i]);
            improved = true;
          }
          else
          {
            x[idA] = xA;
            x[idB] = xB;
          }
        }
      }
    }
  }

  // A horizontal connector run: the mid-height segment of a parent-child edge spans [From, To] in X
  // and sits in generation band <see cref="Level"/> (the lower of the two generations it joins).
  // Every run in a band shares the same Y, so two runs in one band overlap exactly when their X
  // intervals intersect.
  private readonly record struct HorizontalRun(int From, int To, int Level);

  // Builds the horizontal runs indexed both by incident node (the endpoints that move on a swap) and
  // by band (every run a moved run can collide with).
  private static (Dictionary<int, List<HorizontalRun>> ByNode, Dictionary<int, List<HorizontalRun>> ByLevel)
    BuildHorizontalSegments(FamilyTree tree, IReadOnlyDictionary<int, FamilyTreeNode> nodesById)
  {
    var byNode = nodesById.Keys.ToDictionary(id => id, _ => new List<HorizontalRun>());
    var byLevel = new Dictionary<int, List<HorizontalRun>>();
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.ParentChild)
        continue;
      if (!nodesById.TryGetValue(edge.FromId, out var from) || !nodesById.TryGetValue(edge.ToId, out var to))
        continue;
      var level = Math.Min(from.Generation, to.Generation);
      var run = new HorizontalRun(edge.FromId, edge.ToId, level);
      byNode[edge.FromId].Add(run);
      byNode[edge.ToId].Add(run);
      if (!byLevel.TryGetValue(level, out var list))
        byLevel[level] = list = [];
      list.Add(run);
    }
    return (byNode, byLevel);
  }

  // Total overlap fee charged against every run incident to A or B at the current positions. Only
  // these runs move when A and B swap, so comparing this quantity before and after a swap yields the
  // exact change in overlap energy (runs not touching A or B keep their pairwise overlap either way).
  private static double OverlapFee(
    int idA,
    int idB,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByNode,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByLevel,
    IReadOnlyDictionary<int, double> x)
  {
    var fee = 0.0;
    foreach (var run in segByNode[idA])
      fee += RunOverlapAgainstBand(run, segByLevel, x);
    foreach (var run in segByNode[idB])
      fee += RunOverlapAgainstBand(run, segByLevel, x);
    return fee;
  }

  private static double RunOverlapAgainstBand(
    HorizontalRun run,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByLevel,
    IReadOnlyDictionary<int, double> x)
  {
    var aL = Math.Min(x[run.From], x[run.To]);
    var aR = Math.Max(x[run.From], x[run.To]);
    var fee = 0.0;
    foreach (var other in segByLevel[run.Level])
    {
      if (other.From == run.From && other.To == run.To)
        continue;
      var bL = Math.Min(x[other.From], x[other.To]);
      var bR = Math.Max(x[other.From], x[other.To]);
      var overlap = Math.Min(aR, bR) - Math.Max(aL, bL);
      if (overlap > 0)
        fee += OverlapK * overlap;
    }
    return fee;
  }

  // Spring energy of <paramref name="id"/> placed at <paramref name="pos"/>, summing over all
  // incident edges except the one to <paramref name="excludeId"/> plus the anchor penalty.
  private static double NodeEnergy(
    int id,
    double pos,
    IReadOnlyList<(int Id, double W)> neighbors,
    int excludeId,
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, double> anchorX)
  {
    var energy = 0.0;
    foreach (var (neighborId, w) in neighbors)
    {
      if (neighborId == excludeId)
        continue;
      var dx = pos - x[neighborId];
      energy += SpringK * 0.5 * w * dx * dx;
    }
    if (anchorX.TryGetValue(id, out var ax))
    {
      var dx = pos - ax;
      energy += AnchorK * 0.5 * dx * dx;
    }
    return energy;
  }

  private static void SpringRelax(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    IReadOnlyDictionary<int, List<(int Id, double W)>> adj,
    IReadOnlyDictionary<int, double> anchorX)
  {
    var velocity = nodesById.Keys.ToDictionary(id => id, _ => 0.0);
    var rows = nodesById.Values
      .GroupBy(n => n.Generation)
      .ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToList());

    for (var iter = 0; iter < Iterations; iter++)
    {
      var forces = nodesById.Keys.ToDictionary(id => id, _ => 0.0);

      // Edge springs: process each edge once using the id < neighborId convention.
      foreach (var (id, neighbors) in adj)
      {
        foreach (var (neighborId, w) in neighbors)
        {
          if (id >= neighborId)
            continue;
          var dx = x[neighborId] - x[id];
          var f = SpringK * dx * w;
          forces[id] += f;
          forces[neighborId] -= f;
        }
      }

      // Same-row repulsion: linear, drops to zero at RepelRadius slots.
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

      // Anchor: placed nodes resist drifting from their post-swap positions.
      foreach (var (id, ax) in anchorX)
        forces[id] += AnchorK * (ax - x[id]);

      // Semi-implicit Euler.
      foreach (var id in nodesById.Keys)
      {
        velocity[id] = (velocity[id] + forces[id] * TimeStep) * Damping;
        x[id] += velocity[id] * TimeStep;
      }
    }
  }

  // ── tidy-tree seeding helpers ─────────────────────────────────────────────────────────────────

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

  // ── assembly ──────────────────────────────────────────────────────────────────────────────────

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
