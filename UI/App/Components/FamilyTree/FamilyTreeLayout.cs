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
/// A readability score for a finished layout: lower is better. Used to gate the optional refinement
/// pass (so it can never make the chart worse by these measures) and exposed for tests/diagnostics.
/// </summary>
public sealed record FamilyTreeLayoutScore(int Crossings, int Bends, int Misaligned, double TotalLength)
{
  // Crossings hurt comprehension most, then bends, then off-axis (non-straight) parent links; raw
  // pixel length is the gentle tie-breaker that pulls related nodes together.
  public double Total => (Crossings * 1000.0) + (Bends * 10.0) + (Misaligned * 5.0) + (TotalLength * 0.1);
}

/// <summary>
/// Turns a <see cref="FamilyTree"/> into absolute node rectangles and orthogonal connectors.
/// <para>
/// The layout follows three classic phases — layering, horizontal positioning, then edge routing:
/// </para>
/// <list type="number">
/// <item><b>Layering</b> is given for free: a node's <see cref="FamilyTreeNode.Generation"/> fixes its
/// row, so ancestors stack upward and descendants downward.</item>
/// <item><b>Positioning</b> is <i>family-unit aware</i>. Spouses and co-parents (anyone sharing a
/// child) are merged into a single unit so a couple is laid out as one block, never as two unrelated
/// nodes. Two tidy passes — a Reingold–Tilford-style recursive packing — run from the centre: one down
/// over child-units, one up over parent-units. Each pass centres a parent unit over the span of its
/// children (and, going up, fans the parents symmetrically above), so children sit under their couple's
/// midpoint and equivalent subtrees come out mirror-symmetric. A row sweep then removes any residual
/// overlap while keeping unit members adjacent, and an optional median-balance pass straightens spines —
/// kept only if it improves the <see cref="FamilyTreeLayoutScore"/>.</item>
/// <item><b>Routing</b> draws one drop per <i>family</i>, not per edge. All children of a couple share a
/// single vertical trunk dropping from the couple's midpoint to a horizontal sibling bus, from which
/// each child takes a short vertical. A child sitting directly under the trunk is connected by one
/// straight line (zero bends); otherwise the path is the minimal two-bend orthogonal route. Redundant
/// collinear points are collapsed so accidentally-aligned links also become straight.</item>
/// </list>
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

    // Phase 2a: collapse spouses and co-parents into family units so couples lay out as one block.
    var units = FamilyUnits.Build(tree, nodesById);

    var x = new Dictionary<int, double>();

    // Phase 2b: two tidy passes rooted at the centre — descendants packed downward, ancestors packed
    // upward and then slid sideways so the shared centre keeps its column.
    PackDownFromCentre(tree.CenterId, units, x);
    PackUpFromCentre(tree.CenterId, units, x);

    // Collaterals (siblings/cousins) hang off the ancestral line and are reached by neither centre pass;
    // give each a column under its already-placed parents.
    PlaceCollaterals(nodesById, units, x);
    foreach (var node in nodesById.Values)
    {
      x.TryAdd(node.Id, 0);
    }

    // Phase 2c: nudge units apart so members never overlap, preserving the order the packing produced.
    ResolveRowOverlaps(nodesById, units, x);

    // Phase 2d: constrained force refinement (generation-locked, order-preserving), then keep whichever
    // candidate layout scores best — so the refinement can only ever help.
    RefinePositions(tree, nodesById, units, x, metrics);

    return Assemble(tree, nodesById, x, metrics);
  }

  // ---------------------------------------------------------------------------------------------------
  // Phase 2b — tidy packing
  // ---------------------------------------------------------------------------------------------------

  private static void PackDownFromCentre(int centerId, FamilyUnits units, Dictionary<int, double> x)
  {
    var cursor = 0.0;
    var visited = new HashSet<int>();
    Pack(units.UnitOf(centerId), units.ChildUnitsOf, units, x, visited, ref cursor);
  }

  // The ancestor pass is laid out in its own coordinate space, then the whole branch is shifted so the
  // centre keeps the column it already got from the descendant pass.
  private static void PackUpFromCentre(int centerId, FamilyUnits units, Dictionary<int, double> x)
  {
    var centerUnit = units.UnitOf(centerId);
    var up = new Dictionary<int, double>();
    var cursor = 0.0;
    Pack(centerUnit, units.ParentUnitsOf, units, up, new HashSet<int>(), ref cursor);

    if (up.Count == 0)
    {
      return;
    }

    var shift = units.UnitCenter(centerUnit, x) - units.UnitCenter(centerUnit, up);
    foreach (var (id, slot) in up)
    {
      if (units.UnitOf(id) != centerUnit)
      {
        x[id] = slot + shift;
      }
    }
  }

  // Recursive tidy packing of one unit and its subtree in the given direction. Leaves consume successive
  // slots from <paramref name="cursor"/> (so sibling subtrees never overlap); every internal unit is
  // centred over the midpoint of its children's span (so parents sit above the centre of their kids and
  // equal subtrees mirror each other). Returns the centre slot the unit was placed at.
  private static double Pack(
    int unit,
    Func<int, IReadOnlyList<int>> childrenOf,
    FamilyUnits units,
    Dictionary<int, double> x,
    HashSet<int> visited,
    ref double cursor)
  {
    visited.Add(unit);
    var members = units.Members(unit);
    var children = childrenOf(unit).Where(child => !visited.Contains(child)).ToList();

    if (children.Count == 0)
    {
      // Leaf unit: lay its members out side by side at the cursor, then advance past them.
      for (var i = 0; i < members.Count; i++)
      {
        x[members[i]] = cursor + i;
      }

      var leafCentre = cursor + ((members.Count - 1) / 2.0);
      cursor += members.Count;
      return leafCentre;
    }

    var min = double.PositiveInfinity;
    var max = double.NegativeInfinity;
    foreach (var child in children)
    {
      var childCentre = Pack(child, childrenOf, units, x, visited, ref cursor);
      min = Math.Min(min, childCentre);
      max = Math.Max(max, childCentre);
    }

    // Centre the unit's members as a contiguous block over the midpoint of the children's span.
    var centre = (min + max) / 2;
    var left = centre - ((members.Count - 1) / 2.0);
    for (var i = 0; i < members.Count; i++)
    {
      x[members[i]] = left + i;
    }

    return centre;
  }

  private static void PlaceCollaterals(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    FamilyUnits units,
    Dictionary<int, double> x)
  {
    // Walk generations top-down: a node's parents sit one generation up and are placed earlier in this
    // sweep, so a collateral can inherit the average column of its already-placed parents.
    foreach (var node in nodesById.Values.OrderByDescending(node => node.Generation))
    {
      if (x.ContainsKey(node.Id))
      {
        continue;
      }

      var placed = units.ParentsOf(node.Id).Where(x.ContainsKey).Select(parent => x[parent]).ToList();
      if (placed.Count != 0)
      {
        x[node.Id] = placed.Average();
      }
    }
  }

  // ---------------------------------------------------------------------------------------------------
  // Phase 2c — overlap removal
  // ---------------------------------------------------------------------------------------------------

  private static void ResolveRowOverlaps(
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    FamilyUnits units,
    Dictionary<int, double> x)
  {
    foreach (var row in nodesById.Values.GroupBy(node => node.Generation))
    {
      // Order the row unit-by-unit (units kept whole and contiguous) so a couple is never split by an
      // outsider, then push only where members actually collide — leaving the packing's gaps intact.
      var ordered = row
        .GroupBy(node => units.UnitOf(node.Id))
        .Select(group => (Anchor: group.Average(node => x[node.Id]), Unit: group.Key,
          Members: group.Select(node => node.Id).OrderBy(id => id).ToList()))
        .OrderBy(group => group.Anchor)
        .ThenBy(group => group.Unit)
        .SelectMany(group => group.Members)
        .ToList();

      for (var i = 1; i < ordered.Count; i++)
      {
        var minimum = x[ordered[i - 1]] + 1;
        if (x[ordered[i]] < minimum)
        {
          x[ordered[i]] = minimum;
        }
      }
    }
  }

  // ---------------------------------------------------------------------------------------------------
  // Phase 2d — refinement (layered seed -> greedy + constrained force, best score wins)
  // ---------------------------------------------------------------------------------------------------

  // Tunable knobs for the constrained force refinement. Slot units throughout (1 slot == one
  // SlotPitch). Raise the spring/attraction constants to pull harder, raise the repulsion to spread
  // more, raise damping/iterations for a finer settle. The generation lock is absolute (y is never a
  // variable here) and the row-order projection is a hard constraint, so these only shape x within a row.
  private const int ForceIterations = 200;       // how long to let the system settle
  private const double DampingStart = 0.65;      // initial fraction of the net force applied per step
  private const double DampingCooling = 0.973;   // per-iteration cool-down so motion anneals to a stop
  private const double MaxStep = 0.4;            // clamp on per-iteration travel, in slots (stability)
  private const double CenterPull = 0.5;         // family unit -> centroid of its direct-child persons
  private const double SubtreePull = 0.2;        // family unit -> centroid of ALL descendant persons
  private const double UnderPull = 0.45;         // children group -> under their family unit
  private const double SpousePull = 0.4;         // spring toward the preferred spouse/unit-member gap
  private const double SiblingPull = 0.3;        // spring toward even sibling spacing
  private const double CrowdPush = 0.55;         // repulsion when neighbours get closer than desired
  private const double BranchRepulsion = 0.3;    // long-range repulsion between inter-branch row-neighbours
  private const double BranchRepelRadius = 5.0;  // repulsion fades linearly to zero at this many slots
  private const double SpouseDistance = 1.0;     // preferred gap between members of one unit
  private const double SiblingSpacing = 1.0;     // preferred gap between adjacent siblings
  private const double BranchGap = 1.5;          // desired clearance between unrelated neighbouring branches
  private const double MinGap = 1.0;             // hard minimum gap (overlap floor)

  // Builds several candidate layouts from the packed seed and keeps the one with the best
  // <see cref="FamilyTreeLayoutScore"/>: the seed itself, the greedy median pass, the constrained force
  // pass, and force-on-top-of-greedy. Because the seed is always a candidate, refinement can never make
  // the chart worse by our measures; the extra candidates simply give it more ways to improve.
  private static void RefinePositions(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    FamilyUnits units,
    Dictionary<int, double> x,
    FamilyTreeLayoutMetrics metrics)
  {
    var seed = new Dictionary<int, double>(x);

    var greedy = new Dictionary<int, double>(seed);
    RefineColumns(tree, nodesById, greedy);

    var force = new Dictionary<int, double>(seed);
    ForceRefine(tree, nodesById, units, force);

    var greedyThenForce = new Dictionary<int, double>(greedy);
    ForceRefine(tree, nodesById, units, greedyThenForce);

    var best = new[] { seed, greedy, force, greedyThenForce }
      .OrderBy(candidate => ScoreOf(candidate, nodesById, tree, metrics).Total)
      .First();

    foreach (var (id, slot) in best)
    {
      x[id] = slot;
    }
  }

  // Constrained physics refinement. Nodes only ever move horizontally; their generation (row) is fixed,
  // and a per-row order — captured once from the seed — is enforced as a hard minimum-gap constraint, so
  // generations stay aligned, spouses stay adjacent and sibling order never flips. Within those rails the
  // system relaxes a composite energy: family units spring to their direct-child centroid AND their
  // entire subtree centroid (so deep branches align, not just immediate children), children pull back
  // under their unit, unit members spring to compact spouse distance, siblings spring to even spacing,
  // and neighbouring branches repel — hard at MinGap, soft at longer range via BranchRepulsion. Damped.
  private static void ForceRefine(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    FamilyUnits units,
    Dictionary<int, double> x)
  {
    // Descendant persons per family unit, and the parent-unit each child belongs to (co-parents share a
    // unit, so a child maps to a single parent-unit). These do not depend on x, so compute them once.
    var childrenByUnit = new Dictionary<int, List<int>>();
    var parentUnitOfChild = new Dictionary<int, int>();
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.ParentChild
        || !nodesById.ContainsKey(edge.FromId)
        || !nodesById.ContainsKey(edge.ToId))
      {
        continue;
      }

      var parentUnit = units.UnitOf(edge.FromId);
      if (!childrenByUnit.TryGetValue(parentUnit, out var kids))
      {
        childrenByUnit[parentUnit] = kids = [];
      }

      if (!kids.Contains(edge.ToId))
      {
        kids.Add(edge.ToId);
      }

      parentUnitOfChild[edge.ToId] = parentUnit;
    }

    var allUnits = nodesById.Keys.Select(units.UnitOf).Distinct().ToList();

    // All descendant persons for each unit, computed once via BFS through the unit adjacency. Used by
    // SubtreePull so a family unit tracks its entire branch centroid, not just direct children.
    var subtreeDescendants = BuildSubtreeDescendants(allUnits, units);

    // Per-generation order, frozen up front: the projection keeps nodes in exactly this order forever.
    var rows = nodesById.Values
      .GroupBy(node => node.Generation)
      .ToDictionary(
        group => group.Key,
        group => group.OrderBy(node => x[node.Id]).ThenBy(node => node.Id).Select(node => node.Id).ToList());

    var damping = DampingStart;
    for (var iteration = 0; iteration < ForceIterations; iteration++)
    {
      var force = nodesById.Keys.ToDictionary(id => id, _ => 0.0);

      // Family centering: pull the unit toward its children's centroid and slide the child group (rigidly,
      // so their relative spacing is preserved) back under the unit. The two springs meet in the middle,
      // which is exactly "family unit centred over its descendants".
      foreach (var unit in allUnits)
      {
        if (!childrenByUnit.TryGetValue(unit, out var kids))
        {
          continue;
        }

        var present = kids.Where(x.ContainsKey).ToList();
        if (present.Count == 0)
        {
          continue;
        }

        var members = units.Members(unit);
        var unitCentre = (x[members[0]] + x[members[^1]]) / 2;
        var childCentroid = present.Average(child => x[child]);

        var toCentroid = CenterPull * (childCentroid - unitCentre);
        foreach (var member in members)
        {
          force[member] += toCentroid;
        }

        var toUnit = UnderPull * (unitCentre - childCentroid);
        foreach (var child in present)
        {
          force[child] += toUnit;
        }
      }

      // Subtree centroid pull: attract each unit toward the centroid of ALL its descendant persons, not
      // just direct children. CenterPull already handles depth-1; this adds a weaker depth-∞ correction
      // so that a unit over a large lopsided branch gradually drifts toward the branch's true centre of
      // mass. Weaker than CenterPull because the subtree centroid is biased by branch population size.
      foreach (var unit in allUnits)
      {
        if (!subtreeDescendants.TryGetValue(unit, out var descendants))
        {
          continue;
        }

        var present = descendants.Where(x.ContainsKey).ToList();
        if (present.Count == 0)
        {
          continue;
        }

        var members = units.Members(unit);
        var unitCentre = (x[members[0]] + x[members[^1]]) / 2;
        var subtreeCentroid = present.Average(d => x[d]);

        var pull = SubtreePull * (subtreeCentroid - unitCentre);
        foreach (var member in members)
        {
          force[member] += pull;
        }
      }

      // Spouse / unit compactness: members of a unit spring toward the preferred gap so couples stay tight.
      foreach (var unit in allUnits)
      {
        var members = units.Members(unit);
        if (members.Count < 2)
        {
          continue;
        }

        var sorted = members.OrderBy(id => x[id]).ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
          var pull = SpousePull * ((x[sorted[i]] - x[sorted[i - 1]]) - SpouseDistance) * 0.5;
          force[sorted[i - 1]] += pull;
          force[sorted[i]] -= pull;
        }
      }

      // Sibling spacing: adjacent children of a unit spring toward an even gap so a sibling group is laid
      // out regularly rather than bunched.
      foreach (var unit in allUnits)
      {
        if (!childrenByUnit.TryGetValue(unit, out var kids))
        {
          continue;
        }

        var siblings = kids.Where(x.ContainsKey).OrderBy(child => x[child]).ToList();
        for (var i = 1; i < siblings.Count; i++)
        {
          var pull = SiblingPull * ((x[siblings[i]] - x[siblings[i - 1]]) - SiblingSpacing) * 0.5;
          force[siblings[i - 1]] += pull;
          force[siblings[i]] -= pull;
        }
      }

      // Crowding / branch repulsion: neighbours in a row push apart when closer than their desired gap —
      // tighter within a couple, a little looser between unrelated branches so subtrees keep breathing room.
      foreach (var row in rows.Values)
      {
        for (var i = 1; i < row.Count; i++)
        {
          var a = row[i - 1];
          var b = row[i];
          var desired = units.UnitOf(a) == units.UnitOf(b)
            ? SpouseDistance
            : SameParent(parentUnitOfChild, a, b) ? SiblingSpacing : BranchGap;

          var gap = x[b] - x[a];
          if (gap < desired)
          {
            var push = CrowdPush * (desired - gap) * 0.5;
            force[a] -= push;
            force[b] += push;
          }
        }
      }

      // Soft long-range inter-branch repulsion. The hard CrowdPush above fires only once nodes are
      // within desired-gap distance; this fires up to BranchRepelRadius slots away so unrelated branches
      // begin drifting apart earlier and arrive with less accumulated compression. Only non-sibling,
      // different-unit adjacent pairs are repelled — spouses and siblings are handled by their own springs.
      foreach (var row in rows.Values)
      {
        for (var i = 0; i + 1 < row.Count; i++)
        {
          var a = row[i];
          var b = row[i + 1];
          if (units.UnitOf(a) == units.UnitOf(b) || SameParent(parentUnitOfChild, a, b))
          {
            continue;
          }

          var gap = x[b] - x[a];
          if (gap >= BranchRepelRadius)
          {
            continue;
          }

          var strength = BranchRepulsion * (1.0 - gap / BranchRepelRadius);
          force[a] -= strength;
          force[b] += strength;
        }
      }

      // Integrate: horizontal move only, damped and clamped for stability. y is never touched, so the
      // generation lock is exact.
      foreach (var id in nodesById.Keys)
      {
        x[id] += Math.Clamp(damping * force[id], -MaxStep, MaxStep);
      }

      // Project: re-impose the frozen order and the hard minimum gap, removing any overlap the forces
      // introduced without ever reordering a row.
      foreach (var row in rows.Values)
      {
        for (var i = 1; i < row.Count; i++)
        {
          var minimum = x[row[i - 1]] + MinGap;
          if (x[row[i]] < minimum)
          {
            x[row[i]] = minimum;
          }
        }
      }

      damping *= DampingCooling;
    }
  }

  // BFS from each unit downward through the unit adjacency to collect all transitively-reachable
  // descendant persons. Each root gets its own visited set so inbreeding loops are handled safely.
  private static Dictionary<int, List<int>> BuildSubtreeDescendants(
    IReadOnlyList<int> allUnits,
    FamilyUnits units)
  {
    var result = new Dictionary<int, List<int>>();
    foreach (var root in allUnits)
    {
      var persons = new List<int>();
      var visited = new HashSet<int>();
      var queue = new Queue<int>(units.ChildUnitsOf(root));
      while (queue.Count > 0)
      {
        var u = queue.Dequeue();
        if (!visited.Add(u))
        {
          continue;
        }

        persons.AddRange(units.Members(u));
        foreach (var child in units.ChildUnitsOf(u))
        {
          queue.Enqueue(child);
        }
      }

      if (persons.Count > 0)
      {
        result[root] = persons;
      }
    }

    return result;
  }

  private static bool SameParent(IReadOnlyDictionary<int, int> parentUnitOfChild, int a, int b) =>
    parentUnitOfChild.TryGetValue(a, out var pa)
    && parentUnitOfChild.TryGetValue(b, out var pb)
    && pa == pb;

  // How many relaxation sweeps the greedy median candidate runs. Small and convergent on these trees.
  private const int RefinementSweeps = 24;

  // Parent-child links pull harder than the spouse link: were the spouse link heavier, the fee-minimising
  // column of a leaf parent would collapse onto its spouse and ignore its child, so a couple would glue
  // together but never settle over the children they share. Lighter keeps each partner tracking its
  // children while unit adjacency holds the couple together.
  private const double ParentChildWeight = 1.0;
  private const double SpouseWeight = 0.5;

  private readonly record struct WeightedLink(int Id, double Weight);

  // Runs the median-balancing pass on a copy, and keeps it only if the readability score improves. The
  // pass straightens spines (pulling each node toward the weighted median of its relatives) but a frozen
  // row order means it occasionally trades a bend for length; the score gate guarantees it never makes
  // the chart objectively worse than the clean packed seed.
  private static void RefineColumnsGuarded(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    FamilyTreeLayoutMetrics metrics)
  {
    var seed = new Dictionary<int, double>(x);
    RefineColumns(tree, nodesById, x);

    if (ScoreOf(x, nodesById, tree, metrics).Total > ScoreOf(seed, nodesById, tree, metrics).Total)
    {
      foreach (var (id, slot) in seed)
      {
        x[id] = slot;
      }
    }
  }

  // Greedy fee minimisation. Each generation keeps the left-to-right order it was seeded with — so no new
  // crossings can appear — while every node is repeatedly pulled toward the column that minimises its own
  // incident fee (the weighted median of its relatives). Better-connected nodes move first and may shove
  // lighter neighbours aside but never displace an equally- or better-connected one.
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

    var priority = links.ToDictionary(pair => pair.Key, pair => pair.Value.Sum(link => link.Weight));

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

  // The column that minimises a node's own fee is the weighted median of its relatives. We take the
  // midpoint of the median interval so a node pulled equally in two directions lands symmetrically.
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
  // stops at the first node whose priority is at least as high, which never yields, so the row order is
  // never changed.
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
      var blocker = index + 1;
      while (blocker < row.Count && priority[row[blocker]] < self)
      {
        blocker++;
      }

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

  // ---------------------------------------------------------------------------------------------------
  // Phase 3 — assembly and routing
  // ---------------------------------------------------------------------------------------------------

  private static Dictionary<int, Rect> BuildBounds(
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    FamilyTreeLayoutMetrics metrics)
  {
    var maxGeneration = nodesById.Values.Max(node => node.Generation);
    var minSlot = x.Values.Min();

    var bounds = new Dictionary<int, Rect>(nodesById.Count);
    foreach (var node in nodesById.Values)
    {
      var left = metrics.Margin + ((x[node.Id] - minSlot) * metrics.SlotPitch);
      var top = metrics.Margin + ((maxGeneration - node.Generation) * metrics.RowPitch);
      bounds[node.Id] = new Rect(left, top, metrics.NodeWidth, metrics.NodeHeight);
    }

    return bounds;
  }

  private static FamilyTreeLayoutResult Assemble(
    FamilyTree tree,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    Dictionary<int, double> x,
    FamilyTreeLayoutMetrics metrics)
  {
    var bounds = BuildBounds(x, nodesById, metrics);

    var layouts = nodesById.Values
      .Select(node => new FamilyTreeNodeLayout(node, bounds[node.Id]))
      .ToList();

    var connectors = BuildConnectors(tree, bounds, metrics);

    var width = layouts.Max(layout => layout.Bounds.Right) + metrics.Margin;
    var height = layouts.Max(layout => layout.Bounds.Bottom) + metrics.Margin;
    var centerTopLeft = bounds.TryGetValue(tree.CenterId, out var centerRect)
      ? centerRect.Location
      : new Point(0, 0);

    return new FamilyTreeLayoutResult(layouts, connectors, new Size(width, height), centerTopLeft);
  }

  // Routes one connector per family rather than per edge: every child of a couple drops from the same
  // trunk (the couple's midpoint) onto a shared sibling bus, so siblings share segments instead of each
  // fanning its own bent line. A child directly under the trunk gets a single straight vertical.
  private static List<FamilyTreeConnector> BuildConnectors(
    FamilyTree tree,
    IReadOnlyDictionary<int, Rect> bounds,
    FamilyTreeLayoutMetrics metrics)
  {
    var connectors = new List<FamilyTreeConnector>(tree.Edges.Count);

    // Group every child with the full set of its parents present in the chart, so a two-parent child
    // yields one drop from the couple's midpoint instead of two competing diagonals.
    var parentsByChild = new Dictionary<int, List<int>>();
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.ParentChild
        || !bounds.ContainsKey(edge.FromId)
        || !bounds.ContainsKey(edge.ToId))
      {
        continue;
      }

      if (!parentsByChild.TryGetValue(edge.ToId, out var parents))
      {
        parentsByChild[edge.ToId] = parents = [];
      }

      parents.Add(edge.FromId);
    }

    foreach (var (childId, parents) in parentsByChild)
    {
      var child = bounds[childId];
      var parentRects = parents.Select(id => bounds[id]).ToList();

      // The trunk drops from the midpoint of the parents' span (a single parent's own centre), starting
      // at the bottom of the parent row and meeting a sibling bus halfway down to the child row.
      var dropX = (float)((parentRects.Min(rect => rect.Center.X) + parentRects.Max(rect => rect.Center.X)) / 2);
      var parentBottom = (float)parentRects.Max(rect => rect.Bottom);
      var childTop = (float)child.Top;
      var busY = (parentBottom + childTop) / 2f;
      var childX = (float)child.Center.X;

      PointF[] points =
      [
        new PointF(dropX, parentBottom),
        new PointF(dropX, busY),
        new PointF(childX, busY),
        new PointF(childX, childTop),
      ];

      connectors.Add(new FamilyTreeConnector(FamilyTreeRelation.ParentChild, CollapseCollinear(points)));
    }

    // Spouse links stay simple straight horizontals between the partners.
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.Spouse
        || !bounds.TryGetValue(edge.FromId, out var a)
        || !bounds.TryGetValue(edge.ToId, out var b))
      {
        continue;
      }

      connectors.Add(new FamilyTreeConnector(FamilyTreeRelation.Spouse, SpousePath(a, b)));
    }

    return connectors;
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

  // Drops any vertex that lies on the straight line between its neighbours (which also dedupes coincident
  // points). A drop/cross/drop path whose ends share a column therefore collapses to a single straight
  // segment, turning every accidental alignment into a clean bend-free line.
  private static PointF[] CollapseCollinear(PointF[] points)
  {
    if (points.Length <= 2)
    {
      return points;
    }

    var result = new List<PointF>(points.Length) { points[0] };
    for (var i = 1; i < points.Length - 1; i++)
    {
      if (!IsCollinear(result[^1], points[i], points[i + 1]))
      {
        result.Add(points[i]);
      }
    }

    result.Add(points[^1]);
    return result.Count >= 2 ? [.. result] : points;
  }

  private static bool IsCollinear(PointF a, PointF b, PointF c)
  {
    var cross = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
    return MathF.Abs(cross) < 0.01f;
  }

  // ---------------------------------------------------------------------------------------------------
  // Scoring
  // ---------------------------------------------------------------------------------------------------

  /// <summary>Scores a finished layout for diagnostics and tests; lower <see cref="FamilyTreeLayoutScore.Total"/> is better.</summary>
  public static FamilyTreeLayoutScore Score(FamilyTreeLayoutResult result)
  {
    ArgumentNullException.ThrowIfNull(result);
    return ScoreConnectors(result.Connectors);
  }

  private static FamilyTreeLayoutScore ScoreOf(
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, FamilyTreeNode> nodesById,
    FamilyTree tree,
    FamilyTreeLayoutMetrics metrics)
  {
    var bounds = BuildBounds(x, nodesById, metrics);
    return ScoreConnectors(BuildConnectors(tree, bounds, metrics));
  }

  private static FamilyTreeLayoutScore ScoreConnectors(IReadOnlyList<FamilyTreeConnector> connectors)
  {
    var bends = 0;
    var misaligned = 0;
    var totalLength = 0.0;

    foreach (var connector in connectors)
    {
      if (connector.Relation == FamilyTreeRelation.ParentChild)
      {
        var corners = Math.Max(0, connector.Points.Length - 2);
        bends += corners;
        if (corners > 0)
        {
          misaligned++;
        }
      }

      for (var i = 1; i < connector.Points.Length; i++)
      {
        totalLength += Distance(connector.Points[i - 1], connector.Points[i]);
      }
    }

    return new FamilyTreeLayoutScore(CountCrossings(connectors), bends, misaligned, totalLength);
  }

  // Counts proper crossings between parent-child connector segments. O(segments^2) but the charts are
  // small; touching endpoints and overlapping shared trunks (collinear) are deliberately not counted.
  private static int CountCrossings(IReadOnlyList<FamilyTreeConnector> connectors)
  {
    var segments = new List<(PointF A, PointF B)>();
    foreach (var connector in connectors)
    {
      if (connector.Relation != FamilyTreeRelation.ParentChild)
      {
        continue;
      }

      for (var i = 1; i < connector.Points.Length; i++)
      {
        segments.Add((connector.Points[i - 1], connector.Points[i]));
      }
    }

    var crossings = 0;
    for (var i = 0; i < segments.Count; i++)
    {
      for (var j = i + 1; j < segments.Count; j++)
      {
        if (ProperlyIntersect(segments[i].A, segments[i].B, segments[j].A, segments[j].B))
        {
          crossings++;
        }
      }
    }

    return crossings;
  }

  private static bool ProperlyIntersect(PointF a, PointF b, PointF c, PointF d)
  {
    var d1 = Orientation(c, d, a);
    var d2 = Orientation(c, d, b);
    var d3 = Orientation(a, b, c);
    var d4 = Orientation(a, b, d);

    return (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
      && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)));
  }

  private static float Orientation(PointF o, PointF p, PointF q) =>
    ((p.X - o.X) * (q.Y - o.Y)) - ((p.Y - o.Y) * (q.X - o.X));

  private static double Distance(PointF a, PointF b)
  {
    var dx = a.X - b.X;
    var dy = a.Y - b.Y;
    return Math.Sqrt((dx * dx) + (dy * dy));
  }
}
