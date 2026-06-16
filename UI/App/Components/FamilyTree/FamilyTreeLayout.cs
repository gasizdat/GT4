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
/// The overlap term charges a heavy fee whenever two horizontal connector runs share a band: every
/// parent-child edge crossing the same generation pair draws its mid-height run at one shared Y, and
/// every spouse link draws a straight run on its row's centre, so collinear runs (including
/// interleaved spouse pairs) are penalised hard to keep them from stacking on top of each other.
/// This lets nodes find the globally optimal left-to-right ordering within their row —
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

    // ── settle phase ──────────────────────────────────────────────────────────────────────────
    // Relax once before ordering so the reorder pass sees realistic columns. This matters when the
    // centred person is a leaf (e.g. a married-in spouse with no ancestors in view): the tidy seed
    // cannot reach the disconnected branch and collapses its nodes onto one column, leaving nothing
    // to reorder. The spring's same-row repulsion spreads those ties into distinct positions while
    // edge springs keep children under their parents, so the overlap fee reflects the real layout
    // rather than artefacts of the raw seed.
    var settleAnchorX = existing.ToDictionary(id => id, id => x[id]);
    SpringRelax(nodesById, x, adj, settleAnchorX);

    // ── ordering phase ────────────────────────────────────────────────────────────────────────
    // Reorder each row to minimise overlap + spring energy, anchored to settled positions.
    var originalAnchorX = existing.ToDictionary(id => id, id => x[id]);
    ReorderRows(nodesById, x, adj, originalAnchorX, segByNode, segByLevel);

    // ── position phase ────────────────────────────────────────────────────────────────────────
    // Anchor to post-swap positions so the spring refines within the new ordering rather than
    // pulling nodes back to where they were before the swap.
    var springAnchorX = existing.ToDictionary(id => id, id => x[id]);
    SpringRelax(nodesById, x, adj, springAnchorX);

    // The spring has no overlap awareness and its edge forces can re-cross a couple that the ordering
    // pass separated (a child's parent springs can drag it back across its spouse). Re-run the
    // ordering on the settled positions and enforce that final order so it cannot be undone again.
    ReorderRows(nodesById, x, adj, springAnchorX, segByNode, segByLevel);

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

  // Reorders the nodes within each generation to minimise the combined spring + anchor + overlap
  // energy. The row keeps its fixed set of column positions; only which node occupies which column
  // changes, so the row's footprint is preserved while the ordering is optimised.
  //
  // Each step uses an *insertion* move (pull one node out and reinsert it at another column), not a
  // pairwise swap. Swaps are too weak here: when two couples interleave (bro, bro, spouse, spouse),
  // every single swap merely trades the spouse-line overlap for an equal parent-child overlap, so no
  // swap is downhill and the pass stalls. The energy-minimising layout (spouse, bro, bro, spouse) is
  // two swaps away but a *single* insertion — moving a spouse to the outside — reaches it directly.
  // The overlap fee charges OverlapK per slot two connector runs share a band; as it dwarfs the
  // spring terms, clearing overlap dominates the choice.
  private static void ReorderRows(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    IReadOnlyDictionary<int, List<(int Id, double W)>> adj,
    IReadOnlyDictionary<int, double> anchorX,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByNode,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByLevel)
  {
    foreach (var group in nodesById.Values.GroupBy(n => n.Generation))
    {
      var order = group.Select(n => n.Id).OrderBy(id => x[id]).ToList();
      if (order.Count < 2)
        continue;

      // The fixed columns this row occupies; reordering reassigns nodes to them left-to-right.
      var slots = order.Select(id => x[id]).OrderBy(v => v).ToList();
      // Bands whose overlap can change as this row is permuted.
      var bands = order.SelectMany(id => segByNode[id]).Select(r => r.Level).Distinct().ToList();
      ApplyOrder(order, slots, x);

      var improved = true;
      while (improved)
      {
        improved = false;
        var current = RowEnergy(order, x, adj, anchorX, bands, segByLevel);
        var bestGain = 1e-9;
        List<int>? best = null;

        for (var from = 0; from < order.Count; from++)
        {
          for (var to = 0; to < order.Count; to++)
          {
            if (to == from)
              continue;
            var candidate = MoveItem(order, from, to);
            ApplyOrder(candidate, slots, x);
            var gain = current - RowEnergy(candidate, x, adj, anchorX, bands, segByLevel);
            if (gain > bestGain)
            {
              bestGain = gain;
              best = candidate;
            }
          }
        }

        if (best is not null)
        {
          order = best;
          improved = true;
        }
        ApplyOrder(order, slots, x);
      }
    }
  }

  // Assigns the row's fixed columns to its nodes in left-to-right order.
  private static void ApplyOrder(List<int> order, List<double> slots, Dictionary<int, double> x)
  {
    for (var k = 0; k < order.Count; k++)
      x[order[k]] = slots[k];
  }

  // A copy of <paramref name="order"/> with the item at <paramref name="from"/> reinserted at
  // <paramref name="to"/>.
  private static List<int> MoveItem(List<int> order, int from, int to)
  {
    var copy = new List<int>(order);
    var item = copy[from];
    copy.RemoveAt(from);
    copy.Insert(to, item);
    return copy;
  }

  // Energy attributable to a single row's arrangement: spring + anchor over its nodes plus the
  // overlap fee of every band it touches. Absolute value is irrelevant — only differences between
  // permutations of the same row are compared, and the double/single counting of within-row and
  // cross-row edges is identical across those permutations.
  private static double RowEnergy(
    List<int> order,
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, List<(int Id, double W)>> adj,
    IReadOnlyDictionary<int, double> anchorX,
    List<int> bands,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByLevel)
  {
    var energy = 0.0;
    foreach (var id in order)
    {
      foreach (var (neighborId, w) in adj[id])
      {
        var dx = x[id] - x[neighborId];
        energy += SpringK * 0.5 * w * dx * dx;
      }
      if (anchorX.TryGetValue(id, out var ax))
      {
        var dx = x[id] - ax;
        energy += AnchorK * 0.5 * dx * dx;
      }
    }
    foreach (var band in bands)
      energy += BandOverlapFee(band, segByLevel, x);
    return energy;
  }

  // Sum of OverlapK × overlap length over every pair of runs sharing the given band.
  private static double BandOverlapFee(
    int band,
    IReadOnlyDictionary<int, List<HorizontalRun>> segByLevel,
    IReadOnlyDictionary<int, double> x)
  {
    if (!segByLevel.TryGetValue(band, out var runs))
      return 0;
    var fee = 0.0;
    for (var i = 0; i < runs.Count; i++)
    {
      var aL = Math.Min(x[runs[i].From], x[runs[i].To]);
      var aR = Math.Max(x[runs[i].From], x[runs[i].To]);
      for (var j = i + 1; j < runs.Count; j++)
      {
        var bL = Math.Min(x[runs[j].From], x[runs[j].To]);
        var bR = Math.Max(x[runs[j].From], x[runs[j].To]);
        var overlap = Math.Min(aR, bR) - Math.Max(aL, bL);
        if (overlap > 0)
          fee += OverlapK * overlap;
      }
    }
    return fee;
  }

  // A horizontal connector run spanning [From, To] in X within band <see cref="Level"/>. Both the
  // mid-height segment of a parent-child edge and the straight spouse link are horizontal, so both
  // are runs. Every run in a band shares the same Y, so two runs in one band overlap exactly when
  // their X intervals intersect.
  private readonly record struct HorizontalRun(int From, int To, int Level);

  // Builds the horizontal runs indexed both by incident node (the endpoints that move on a swap) and
  // by band (every run a moved run can collide with). Parent-child runs sit in the band between two
  // generations; spouse runs sit on a generation's row centre — a distinct Y. The band key encodes
  // both kinds into one keyspace (even = parent-child band, odd = spouse band) so a parent-child run
  // and a spouse run are never mistaken for sharing a Y.
  private static (Dictionary<int, List<HorizontalRun>> ByNode, Dictionary<int, List<HorizontalRun>> ByLevel)
    BuildHorizontalSegments(FamilyTree tree, IReadOnlyDictionary<int, FamilyTreeNode> nodesById)
  {
    var byNode = nodesById.Keys.ToDictionary(id => id, _ => new List<HorizontalRun>());
    var byLevel = new Dictionary<int, List<HorizontalRun>>();

    void Add(int fromId, int toId, int level)
    {
      var run = new HorizontalRun(fromId, toId, level);
      byNode[fromId].Add(run);
      byNode[toId].Add(run);
      if (!byLevel.TryGetValue(level, out var list))
        byLevel[level] = list = [];
      list.Add(run);
    }

    foreach (var edge in tree.Edges)
    {
      if (!nodesById.TryGetValue(edge.FromId, out var from) || !nodesById.TryGetValue(edge.ToId, out var to))
        continue;
      if (edge.Relation == FamilyTreeRelation.ParentChild)
        Add(edge.FromId, edge.ToId, 2 * Math.Min(from.Generation, to.Generation));
      else
        Add(edge.FromId, edge.ToId, (2 * from.Generation) + 1);
    }
    return (byNode, byLevel);
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
