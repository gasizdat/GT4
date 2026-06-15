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
/// Descendants and ancestors are laid out as two tidy trees that share the centred person as their
/// root: leaves take successive horizontal slots and each parent is centred over its children.
/// Spouses are pulled in beside their partner, then every generation row is swept left-to-right to
/// remove any residual overlap. That seeds a stable left-to-right order, which a final greedy pass
/// refines by repeatedly pulling each node toward the column that minimises its connector fee (the
/// weighted span of its links) — shortening the connectors so a married couple straddles the centre
/// of their children instead of one partner fanning links across the whole row.
/// </para>
/// </summary>
public static class FamilyTreeLayout
{
  public static FamilyTreeLayoutResult Build(FamilyTree tree, FamilyTreeLayoutMetrics metrics)
  {
    ArgumentNullException.ThrowIfNull(tree);
    ArgumentNullException.ThrowIfNull(metrics);

    if (tree.Nodes.Count == 0)
    {
      return new FamilyTreeLayoutResult([], [], new Size(0, 0), new Point(0, 0));
    }

    var nodesById = tree.Nodes.ToDictionary(node => node.Id);
    var childrenByParent = BuildAdjacency(tree, nodesById, descendants: true);
    var parentsByChild = BuildAdjacency(tree, nodesById, descendants: false);

    var x = new Dictionary<int, double>();

    // Two independent tidy passes rooted at the centre, then aligned on the centre's column.
    LayoutBranch(tree.CenterId, childrenByParent, x);
    AlignBranch(tree.CenterId, parentsByChild, x);

    // Collaterals (siblings/cousins) hang off the ancestral line, so the centre-rooted passes never
    // reach them; give each a column under its parents.
    PlaceCollaterals(nodesById, parentsByChild, x);

    PlaceSpouses(tree, nodesById, x, metrics);
    ResolveRowOverlaps(nodesById, x, metrics);

    // The passes above fix a sensible left-to-right order per generation; this shortens the links
    // within that order.
    RefineColumns(tree, nodesById, x);

    return Assemble(tree, nodesById, x, metrics);
  }

  private static Dictionary<int, List<int>> BuildAdjacency(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    bool descendants)
  {
    var adjacency = new Dictionary<int, List<int>>();

    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.ParentChild)
      {
        continue;
      }

      // Descendant pass walks parent -> child (downward); ancestor pass walks child -> parent (upward).
      var (key, value) = descendants ? (edge.FromId, edge.ToId) : (edge.ToId, edge.FromId);
      if (!nodesById.ContainsKey(key) || !nodesById.ContainsKey(value))
      {
        continue;
      }

      if (!adjacency.TryGetValue(key, out var list))
      {
        adjacency[key] = list = [];
      }

      list.Add(value);
    }

    return adjacency;
  }

  // First pass: assign an absolute slot to the centre and one whole branch (descendants).
  private static void LayoutBranch(int rootId, Dictionary<int, List<int>> childrenOf, Dictionary<int, double> x)
  {
    var cursor = 0.0;
    var visited = new HashSet<int>();
    AssignSlots(rootId, childrenOf, x, visited, ref cursor);
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
      ? list.Where(child => !visited.Contains(child)).ToList()
      : [];

    if (children.Count == 0)
    {
      x[id] = cursor;
      cursor += 1;
      return x[id];
    }

    var sum = 0.0;
    foreach (var child in children)
    {
      sum += AssignSlots(child, childrenOf, x, visited, ref cursor);
    }

    x[id] = sum / children.Count;
    return x[id];
  }

  // Second pass: lay out the other branch (ancestors) relative to its own root, then slide the whole
  // branch sideways so the shared centre keeps the column it already got from the first pass.
  private static void AlignBranch(int rootId, Dictionary<int, List<int>> parentsOf, Dictionary<int, double> x)
  {
    var branchX = new Dictionary<int, double>();
    var cursor = 0.0;
    AssignSlots(rootId, parentsOf, branchX, [], ref cursor);

    var shift = x[rootId] - branchX[rootId];
    foreach (var (id, slot) in branchX)
    {
      if (id == rootId)
      {
        continue;
      }

      x[id] = slot + shift;
    }
  }

  private static void PlaceCollaterals(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, List<int>> parentsByChild,
    Dictionary<int, double> x)
  {
    // Walk generations top-down: a node's parents sit one generation up and are therefore placed
    // earlier in this sweep, so a collateral can inherit the average column of its placed parents.
    foreach (var node in nodesById.Values.OrderByDescending(node => node.Generation))
    {
      if (x.ContainsKey(node.Id))
      {
        continue;
      }

      if (parentsByChild.TryGetValue(node.Id, out var parents))
      {
        var placed = parents.Where(x.ContainsKey).Select(parent => x[parent]).ToList();
        if (placed.Count != 0)
        {
          x[node.Id] = placed.Average();
        }
      }
    }
  }

  private static void PlaceSpouses(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    FamilyTreeLayoutMetrics metrics)
  {
    // A spouse with no slot of its own is parked one column to the right of the partner that has one.
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.Spouse)
      {
        continue;
      }

      var aPlaced = x.ContainsKey(edge.FromId);
      var bPlaced = x.ContainsKey(edge.ToId);

      if (aPlaced && !bPlaced && nodesById.ContainsKey(edge.ToId))
      {
        x[edge.ToId] = x[edge.FromId] + 1;
      }
      else if (bPlaced && !aPlaced && nodesById.ContainsKey(edge.FromId))
      {
        x[edge.FromId] = x[edge.ToId] + 1;
      }
    }

    // Any node still unplaced (disconnected spouse chains) falls back to the far right of its row.
    foreach (var node in nodesById.Values)
    {
      x.TryAdd(node.Id, 0);
    }
  }

  private static void ResolveRowOverlaps(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    FamilyTreeLayoutMetrics metrics)
  {
    var rows = nodesById.Values.GroupBy(node => node.Generation);

    foreach (var row in rows)
    {
      var ordered = row.OrderBy(node => x[node.Id]).ThenBy(node => node.Id).ToList();
      for (var i = 1; i < ordered.Count; i++)
      {
        var previous = x[ordered[i - 1].Id];
        var current = x[ordered[i].Id];
        if (current - previous < 1)
        {
          x[ordered[i].Id] = previous + 1;
        }
      }
    }
  }

  // How many relaxation sweeps to run. The trees are small (a few dozen nodes per row at most) and the
  // greedy pass converges quickly, so a modest fixed budget keeps the result stable and cheap.
  private const int RefinementSweeps = 24;

  // Partners are already kept side by side by the frozen row order (the seeding pass parks a spouse
  // right next to its mate and nothing is ever reordered between them), so the spouse link only needs a
  // gentle pull. It is deliberately lighter than a parent-child link: were it heavier, the fee-minimising
  // column (a weighted median) of a leaf parent would collapse onto its spouse and ignore its child, so
  // a couple would glue together but never settle over the children they share. Lighter keeps each
  // partner tracking its children while adjacency holds the couple together.
  private const double ParentChildWeight = 1.0;
  private const double SpouseWeight = 0.5;

  /// <summary>One relative of a node together with the fee charged per slot of horizontal separation.</summary>
  private readonly record struct WeightedLink(int Id, double Weight);

  // Greedy fee minimisation. The "fee" of a connector is its horizontal span (in slots) times the
  // weight of its relation, and the total fee is what we want to drive down. Each generation keeps the
  // left-to-right order it was seeded with — so no new line crossings can appear — while every node is
  // repeatedly pulled toward the column that minimises its own incident fee (the weighted median of its
  // relatives). Better-connected nodes move first and may shove lighter neighbours aside but never
  // displace an equally- or better-connected one, so the spine straightens and in-married spouses and
  // leaf parents collapse onto their children instead of trailing long links across the row.
  private static void RefineColumns(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x)
  {
    var links = nodesById.Keys.ToDictionary(id => id, _ => new List<WeightedLink>());
    foreach (var edge in tree.Edges)
    {
      if (!links.ContainsKey(edge.FromId) || !links.ContainsKey(edge.ToId))
      {
        continue;
      }

      var weight = edge.Relation == FamilyTreeRelation.Spouse ? SpouseWeight : ParentChildWeight;
      links[edge.FromId].Add(new WeightedLink(edge.ToId, weight));
      links[edge.ToId].Add(new WeightedLink(edge.FromId, weight));
    }

    // A node's "priority" is its total link weight: the heavier it is connected, the more it costs to
    // leave it mis-aligned, so it gets first pick of its column and the right to push lighter nodes.
    var priority = links.ToDictionary(pair => pair.Key, pair => pair.Value.Sum(link => link.Weight));

    // Process generations top-down then bottom-up on alternating sweeps so pull from above and below
    // balances out. The per-row order is frozen up front from the seeding passes' columns.
    var generations = nodesById.Values
      .Select(node => node.Generation)
      .Distinct()
      .OrderByDescending(generation => generation)
      .ToList();

    var rows = generations.ToDictionary(
      generation => generation,
      generation => nodesById.Values
        .Where(node => node.Generation == generation)
        .OrderBy(node => x[node.Id])
        .ThenBy(node => node.Id)
        .Select(node => node.Id)
        .ToList());

    for (var sweep = 0; sweep < RefinementSweeps; sweep++)
    {
      var order = sweep % 2 == 0 ? generations : Enumerable.Reverse(generations);
      foreach (var generation in order)
      {
        var row = rows[generation];
        // Heaviest first: they claim their fee-minimising column before lighter nodes settle around them.
        var byPriority = Enumerable.Range(0, row.Count)
          .OrderByDescending(index => priority[row[index]])
          .ToList();

        foreach (var index in byPriority)
        {
          var relatives = links[row[index]];
          if (relatives.Count != 0)
          {
            MoveToward(row, priority, x, index, WeightedMedian(relatives, x));
          }
        }
      }
    }
  }

  // The column that minimises a node's own fee is the weighted median of its relatives: an L1 cost is
  // smallest at the median, where half the link weight sits on either side. We take the midpoint of the
  // median interval so a node pulled equally in two directions lands symmetrically between them. Because
  // parent-child links outweigh the spouse link, a leaf parent's median resolves onto its child's
  // column, so both partners of a couple settle over the children they share.
  private static double WeightedMedian(IReadOnlyList<WeightedLink> relatives, IReadOnlyDictionary<int, double> x)
  {
    var ordered = relatives.OrderBy(link => x[link.Id]).ToList();
    var half = ordered.Sum(link => link.Weight) / 2;

    var lower = x[ordered[0].Id];
    var cumulative = 0.0;
    foreach (var link in ordered)
    {
      cumulative += link.Weight;
      if (cumulative >= half)
      {
        lower = x[link.Id];
        break;
      }
    }

    var upper = x[ordered[^1].Id];
    cumulative = 0.0;
    for (var i = ordered.Count - 1; i >= 0; i--)
    {
      cumulative += ordered[i].Weight;
      if (cumulative >= half)
      {
        upper = x[ordered[i].Id];
        break;
      }
    }

    return (lower + upper) / 2;
  }

  // Slides the node at <paramref name="index"/> toward <paramref name="target"/> as far as the spacing
  // allows. Lighter nodes in the way are shoved along (keeping one slot between neighbours); the move
  // stops at the first node whose priority is at least as high, which never yields. The row order is
  // therefore never changed.
  private static void MoveToward(
    IReadOnlyList<int> row,
    IReadOnlyDictionary<int, double> priority,
    Dictionary<int, double> x,
    int index,
    double target)
  {
    var self = priority[row[index]];
    var current = x[row[index]];

    if (target > current)
    {
      // Find the first equal-or-heavier node to the right; everything before it can be pushed.
      var blocker = index + 1;
      while (blocker < row.Count && priority[row[blocker]] < self)
      {
        blocker++;
      }

      // Leave room for the pushed nodes to keep one slot each up to the blocker.
      var limit = blocker < row.Count ? x[row[blocker]] - (blocker - index) : double.PositiveInfinity;
      var moved = Math.Min(target, limit);
      if (moved <= current)
      {
        return;
      }

      x[row[index]] = moved;
      for (var j = index + 1; j < blocker; j++)
      {
        var minimum = x[row[j - 1]] + 1;
        if (x[row[j]] >= minimum)
        {
          break;
        }

        x[row[j]] = minimum;
      }
    }
    else if (target < current)
    {
      var blocker = index - 1;
      while (blocker >= 0 && priority[row[blocker]] < self)
      {
        blocker--;
      }

      var limit = blocker >= 0 ? x[row[blocker]] + (index - blocker) : double.NegativeInfinity;
      var moved = Math.Max(target, limit);
      if (moved >= current)
      {
        return;
      }

      x[row[index]] = moved;
      for (var j = index - 1; j > blocker; j--)
      {
        var maximum = x[row[j + 1]] - 1;
        if (x[row[j]] <= maximum)
        {
          break;
        }

        x[row[j]] = maximum;
      }
    }
  }

  private static FamilyTreeLayoutResult Assemble(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    FamilyTreeLayoutMetrics metrics)
  {
    var maxGeneration = nodesById.Values.Max(node => node.Generation);
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

    var width = layouts.Max(layout => layout.Bounds.Right) + metrics.Margin;
    var height = layouts.Max(layout => layout.Bounds.Bottom) + metrics.Margin;
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
      {
        continue;
      }

      var points = edge.Relation == FamilyTreeRelation.ParentChild
        ? ParentChildPath(parent: from, child: to)
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

    // Drop from the parent, run across at the midpoint, then drop into the child: two right-angle bends.
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
