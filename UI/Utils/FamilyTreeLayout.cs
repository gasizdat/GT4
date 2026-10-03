using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI.Utils;

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

/// <summary>
/// An orthogonal poly-line, ready to be stroked with rounded bends. A loop draws a relationship the rows
/// cannot hold, such as a parent that pedigree collapse put on its child's own row.
/// </summary>
public sealed record FamilyTreeConnector(FamilyTreeRelation Relation, PointF[] Points, bool IsLoop = false);

public sealed record FamilyTreeLayoutResult(
  IReadOnlyList<FamilyTreeNodeLayout> Nodes,
  IReadOnlyList<FamilyTreeConnector> Connectors,
  Size CanvasSize,
  Point CenterTopLeft,
  FamilyTreeLayoutMetrics Metrics);

/// <summary>
/// Turns a <see cref="FamilyTree"/> into node rectangles and pedigree-chart connectors.
/// <para>
/// Children are grouped into families by the parents they have in the row above. Positions come from a
/// tidy, contour-packed layout in slots (1 slot = <see cref="FamilyTreeLayoutMetrics.SlotPitch"/>): each
/// subtree packs beside its neighbours as closely as their row-by-row extents allow, a family's children
/// are centred under its drop, and every direct-line ancestor sits exactly over the child it descends to.
/// The father's relatives go outboard to the left, the mother's to the right. Each person is placed once,
/// so a relationship the placement cannot hold, as with pedigree collapse, is drawn as a loop.
/// </para>
/// <para>
/// Each family is drawn once: a marriage line with one drop from its midpoint, or a drop from each parent
/// when they have no marriage on record, then one sibship bar and a stub per child. Horizontal runs that
/// would overlap in the band between two rows get separate tracks there.
/// </para>
/// </summary>
public static class FamilyTreeLayout
{
  // Absorbs the rounding in a neighbouring slot computed as taken ± 1.
  private const double SlotTolerance = 1e-9;

  /// <param name="pins">
  /// Slot offsets from the centre person for nodes the user placed by hand. When any apply, the centre
  /// stays where the layout put it, and each pinned node keeps its offset unless the centre or an earlier
  /// pin in its row already holds that slot, in which case it takes the nearest clear one. Every other
  /// node keeps its order and moves only as far as it must to stay a slot clear of the pins.
  /// </param>
  public static FamilyTreeLayoutResult Compute(
    FamilyTree tree,
    FamilyTreeLayoutMetrics metrics,
    IReadOnlyDictionary<int, double>? pins = null)
  {
    ArgumentNullException.ThrowIfNull(tree);
    ArgumentNullException.ThrowIfNull(metrics);

    if (tree.Nodes.Count == 0)
      return new FamilyTreeLayoutResult([], [], new Size(0, 0), new Point(0, 0), metrics);

    var families = new Families(tree);
    var placement = new Placement(families, new Dictionary<int, Hint>());
    placement.Run(tree.CenterId);

    // kinship2's autohint: married blood relatives in one row move to the facing ends of their sibships.
    var hints = FacingEnds(families, placement.X);
    if (hints.Count != 0)
    {
      placement = new Placement(families, hints);
      placement.Run(tree.CenterId);
    }

    var x = placement.X;
    var pinned = ResolvePins(tree.CenterId, families, x, pins);
    if (pinned.Count != 0)
    {
      foreach (var (id, slot) in pinned)
        x[id] = slot;
      ClearPins(families, x, pinned);
    }

    return Assemble(tree.CenterId, families, x, placement.PlacedBy, metrics);
  }

  /// <summary>
  /// The slot nearest <paramref name="slot"/>, no lower than <paramref name="minimum"/>, that keeps a
  /// whole slot clear of every <paramref name="taken"/> one.
  /// </summary>
  public static double NearestClearSlot(double slot, double[] taken, double minimum = double.NegativeInfinity)
  {
    // The clear region is bounded by the minimum and the taken slots' neighbours, so the nearest clear
    // slot is the one asked for (raised to the minimum) or one of those neighbours.
    bool IsClear(double candidate) =>
      candidate >= minimum && taken.All(t => Math.Abs(candidate - t) >= 1 - SlotTolerance);

    var wanted = Math.Max(slot, minimum);
    return taken
      .SelectMany(t => new[] { t - 1, t + 1 })
      .Prepend(wanted)
      .Where(IsClear)
      .MinBy(candidate => Math.Abs(candidate - slot));
  }

  private enum Side { Left, Right, Both }

  // Which end of their sibship a person should take to face the spouse they married from another one.
  private readonly record struct Hint(int Direction, int Partner);

  private static (bool, int, int) BirthOrder(FamilyTreeNode node) =>
    (node.Person.BirthDate.Status == DateStatus.Unknown, node.Person.BirthDate.Code, node.Id);

  private static string FamilyKey(IEnumerable<int> parents) => string.Join(",", parents.Order());

  // ── structure ─────────────────────────────────────────────────────────────────────────────────────

  // The tree's families: a child belongs to the family of the parents it has in the row above. A parent
  // in any other row, and a spouse in another row, is a relationship the rows cannot hold.
  private sealed class Families
  {
    private readonly Dictionary<int, int[]> _ParentsOf;
    private readonly Dictionary<string, int[]> _Partners;
    private readonly Dictionary<string, int[]> _Children;
    private readonly Dictionary<int, string[]> _KeysOf;
    private readonly Dictionary<int, int[]> _SpousesOf;
    private readonly Dictionary<int, int[]> _Rows;

    public Families(FamilyTree tree)
    {
      Nodes = tree.Nodes.ToDictionary(node => node.Id);
      var edges = tree.Edges
        .Where(edge => Nodes.ContainsKey(edge.FromId) && Nodes.ContainsKey(edge.ToId))
        .Distinct()
        .OrderBy(edge => edge.FromId)
        .ThenBy(edge => edge.ToId)
        .ToArray();
      var parentEdges = edges.Where(edge => edge.Relation == FamilyTreeRelation.ParentChild).ToArray();
      var spouseEdges = edges.Where(edge => edge.Relation == FamilyTreeRelation.Spouse).ToArray();

      bool InRow(FamilyTreeEdge edge) => Generation(edge.FromId) == Generation(edge.ToId) + 1;
      bool SameRow(FamilyTreeEdge edge) => Generation(edge.FromId) == Generation(edge.ToId);

      _ParentsOf = parentEdges
        .Where(InRow)
        .GroupBy(edge => edge.ToId)
        .ToDictionary(group => group.Key, group => group.Select(edge => edge.FromId).Order().ToArray());
      OffRowParents = [.. parentEdges.Where(edge => !InRow(edge)).Select(edge => (edge.FromId, edge.ToId))];
      var childrenByKey = _ParentsOf.GroupBy(pair => FamilyKey(pair.Value));
      _Children = childrenByKey.ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).Order().ToArray());
      _Partners = childrenByKey.ToDictionary(group => group.Key, group => group.First().Value);
      Keys = [.. _Partners.Keys.Order(StringComparer.Ordinal)];
      _KeysOf = Keys
        .SelectMany(key => _Partners[key].Select(partner => (Partner: partner, Key: key)))
        .GroupBy(pair => pair.Partner)
        .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).ToArray());
      Spouses = [.. spouseEdges.Select(edge => (edge.FromId, edge.ToId))];
      _SpousesOf = spouseEdges
        .Where(SameRow)
        .SelectMany(edge => new[] { (edge.FromId, edge.ToId), (edge.ToId, edge.FromId) })
        .GroupBy(pair => pair.Item1)
        .ToDictionary(group => group.Key, group => group.Select(pair => pair.Item2).Order().ToArray());
      _Rows = Nodes.Values
        .GroupBy(node => node.Generation)
        .ToDictionary(group => group.Key, group => group.Select(node => node.Id).Order().ToArray());
    }

    public IReadOnlyDictionary<int, FamilyTreeNode> Nodes { get; }

    public string[] Keys { get; }

    public (int Parent, int Child)[] OffRowParents { get; }

    public (int A, int B)[] Spouses { get; }

    public int Generation(int id) => Nodes[id].Generation;

    public int[] ParentsOf(int child) => _ParentsOf.GetValueOrDefault(child, []);

    public string? KeyOf(int child) => _ParentsOf.TryGetValue(child, out var parents) ? FamilyKey(parents) : null;

    public int[] Partners(string key) => _Partners[key];

    public int[] ChildrenOf(string key) => _Children[key];

    public string[] KeysOf(int partner) => _KeysOf.GetValueOrDefault(partner, []);

    public int[] SpousesOf(int id) => _SpousesOf.GetValueOrDefault(id, []);

    public int[] Row(int generation) => _Rows.GetValueOrDefault(generation, []);

    public bool AreSpouses(int a, int b) => SpousesOf(a).Contains(b);
  }

  private static bool IsClear(Families families, IReadOnlyDictionary<int, double> x, int a, int b, int[] except)
  {
    var low = Math.Min(x[a], x[b]);
    var high = Math.Max(x[a], x[b]);
    var row = families.Row(families.Generation(a));
    return !row.Any(id => !except.Contains(id) && x[id] > low + SlotTolerance && x[id] < high - SlotTolerance);
  }

  private static Dictionary<int, Hint> FacingEnds(Families families, IReadOnlyDictionary<int, double> x)
  {
    var hints = new Dictionary<int, Hint>();
    foreach (var (a, b) in families.Spouses)
    {
      var blood = families.ParentsOf(a).Length != 0 && families.ParentsOf(b).Length != 0;
      if (!blood || families.Generation(a) != families.Generation(b) || IsClear(families, x, a, b, [a, b]))
        continue;
      var direction = Math.Sign(x[b] - x[a]);
      hints.TryAdd(a, new Hint(direction, b));
      hints.TryAdd(b, new Hint(-direction, a));
    }
    return hints;
  }

  // ── placement ─────────────────────────────────────────────────────────────────────────────────────

  // A placed subtree: the x of every person in it and its leftmost and rightmost x on each row.
  private sealed class Shape
  {
    private readonly Dictionary<int, (double Left, double Right)> _Rows = [];

    public Dictionary<int, double> X { get; } = [];

    public void Place(int id, int generation, double x)
    {
      X[id] = x;
      _Rows[generation] = _Rows.TryGetValue(generation, out var row)
        ? (Math.Min(row.Left, x), Math.Max(row.Right, x))
        : (x, x);
    }

    public void Absorb(Shape other, double dx)
    {
      foreach (var (id, x) in other.X)
        X[id] = x + dx;
      foreach (var (generation, (left, right)) in other._Rows)
      {
        _Rows[generation] = _Rows.TryGetValue(generation, out var row)
          ? (Math.Min(row.Left, left + dx), Math.Max(row.Right, right + dx))
          : (left + dx, right + dx);
      }
    }

    // How far right other must move to stay a slot clear of this shape on every row they share; -∞ when
    // they share none.
    public double ClearanceTo(Shape other)
    {
      var clearance = double.NegativeInfinity;
      foreach (var (generation, row) in _Rows)
      {
        if (other._Rows.TryGetValue(generation, out var next))
          clearance = Math.Max(clearance, row.Right + 1 - next.Left);
      }
      return clearance;
    }

    // Beside it on the right, even when the two share no row.
    public double OffsetBeside(Shape other)
    {
      var clearance = ClearanceTo(other);
      if (!double.IsNegativeInfinity(clearance))
        return clearance;
      var right = _Rows.Values.Max(row => row.Right);
      var left = other._Rows.Values.Min(row => row.Left);
      return right + 1 - left;
    }

    public void Append(Shape other)
    {
      var dx = X.Count == 0 ? 0 : ClearanceTo(other);
      Absorb(other, dx);
    }
  }

  private sealed class Placement
  {
    private readonly Families _Families;
    private readonly IReadOnlyDictionary<int, Hint> _Hints;
    private readonly HashSet<int> _Visited = [];
    // The centre and its ancestors: only the ancestry walk places them, never a collateral's subtree.
    private readonly HashSet<int> _DirectLine = [];

    public Placement(Families families, IReadOnlyDictionary<int, Hint> hints)
    {
      _Families = families;
      _Hints = hints;
    }

    public Dictionary<int, double> X { get; private set; } = [];

    // The family whose sibship placed each child. A child placed elsewhere hangs from its family by a loop.
    public Dictionary<int, string> PlacedBy { get; } = [];

    public void Run(int centerId)
    {
      var ancestors = new Stack<int>([centerId]);
      _DirectLine.Add(centerId);
      while (ancestors.Count != 0)
      {
        var person = ancestors.Pop();
        foreach (var parent in _Families.ParentsOf(person).Where(_DirectLine.Add))
          ancestors.Push(parent);
      }

      _Visited.Add(centerId);
      var own = Block(centerId, Side.Both, excludedKey: null);
      var shape = Ancestry(centerId, Side.Both, own);

      var rest = _Families.Nodes.Values.OrderByDescending(node => node.Generation).ThenBy(node => node.Id);
      foreach (var node in rest)
      {
        if (!_Visited.Add(node.Id))
          continue;
        var part = Block(node.Id, Side.Both, excludedKey: null);
        var dx = shape.OffsetBeside(part);
        shape.Absorb(part, dx);
      }

      X = shape.X;
    }

    private bool IsFree(int id) => !_Visited.Contains(id) && !_DirectLine.Contains(id);

    private int HintRank(int id) => _Hints.TryGetValue(id, out var hint) ? hint.Direction + 1 : 1;

    private IEnumerable<int> Ordered(IEnumerable<int> ids) =>
      ids.OrderBy(id => (HintRank(id), BirthOrder(_Families.Nodes[id])));

    // The person with the partners who join them on their row, and below them each family's children.
    // A partner on either side of the person is spread out until their family's drop is over its children.
    private Shape Block(int person, Side side, string? excludedKey)
    {
      var generation = _Families.Generation(person);
      int[] excludedPartners = excludedKey is null ? [] : _Families.Partners(excludedKey);
      var keys = _Families
        .KeysOf(person)
        .Where(key => key != excludedKey && !_Families.Partners(key).Any(p => p != person && excludedPartners.Contains(p)))
        .ToArray();
      var partners = _Families
        .SpousesOf(person)
        .Concat(keys.SelectMany(_Families.Partners))
        .Where(p => p != person && IsFree(p) && _Families.ParentsOf(p).Length == 0)
        .Distinct()
        .OrderBy(p => BirthOrder(_Families.Nodes[p]))
        .ToArray();
      foreach (var partner in partners)
        _Visited.Add(partner);

      var left = new List<int>();
      var right = new List<int>();
      var preferRight = _Families.Nodes[person].Person.BiologicalSex != BiologicalSex.Female;
      for (var i = 0; i < partners.Length; i++)
      {
        var toRight = side switch
        {
          Side.Left => false,
          Side.Right => true,
          _ => (i % 2 == 0) == preferRight,
        };
        (toRight ? right : left).Add(partners[i]);
      }

      // A married blood relative sits in their own sibship; when they were hinted to face this person,
      // their family is laid out as if they were already beside them.
      _Hints.TryGetValue(person, out var hint);
      string? SideKey(List<int> sidePartners, int direction)
      {
        if (sidePartners.Count != 0)
          return FamilyKey([person, sidePartners[0]]);
        return hint.Direction == direction ? FamilyKey([person, hint.Partner]) : null;
      }
      var leftKey = SideKey(left, -1);
      var rightKey = SideKey(right, +1);

      var leftGroup = Group(keys.Where(key => key == leftKey));
      var middleGroup = Group(keys.Where(key => key != leftKey && key != rightKey));
      var rightGroup = Group(keys.Where(key => key == rightKey));

      var shape = new Shape();
      double? leftCentre = null;
      double? middleCentre = null;
      double? rightCentre = null;
      if (leftGroup is not null)
      {
        shape.Append(leftGroup.Value.Shape);
        leftCentre = Centre(shape, leftGroup.Value.Children);
      }
      if (middleGroup is not null)
      {
        shape.Append(middleGroup.Value.Shape);
        middleCentre = Centre(shape, middleGroup.Value.Children);
      }
      if (rightGroup is not null)
      {
        shape.Append(rightGroup.Value.Shape);
        rightCentre = Centre(shape, rightGroup.Value.Children);
      }

      // A hinted partner is not in the block, so its family's drop is pinned half a slot off the person.
      var x = middleCentre ?? (leftCentre, rightCentre) switch
      {
        (double l, double r) when left.Count != 0 && right.Count != 0 => (l + r) / 2,
        (double l, double r) => left.Count == 0 ? l + 0.5 : r - 0.5,
        (double l, null) => l + 0.5,
        (null, double r) => r - 0.5,
        _ => 0,
      };
      shape.Place(person, generation, x);

      var leftX = leftCentre is double lc && left.Count != 0 ? (2 * lc) - x : x - 1;
      for (var i = 0; i < left.Count; i++)
        shape.Place(left[i], generation, leftX - i);
      var rightX = rightCentre is double rc && right.Count != 0 ? (2 * rc) - x : x + 1;
      for (var i = 0; i < right.Count; i++)
        shape.Place(right[i], generation, rightX + i);

      return shape;
    }

    private (Shape Shape, int[] Children)? Group(IEnumerable<string> keys)
    {
      var shape = new Shape();
      var children = new List<int>();
      foreach (var key in keys)
      {
        foreach (var child in Ordered(_Families.ChildrenOf(key)))
        {
          if (!IsFree(child))
            continue;
          _Visited.Add(child);
          PlacedBy[child] = key;
          children.Add(child);
          var subtree = Block(child, Side.Both, excludedKey: null);
          shape.Append(subtree);
        }
      }
      return children.Count == 0 ? null : (shape, [.. children]);
    }

    private static double Centre(Shape shape, int[] children)
    {
      var xs = children.Select(child => shape.X[child]).ToArray();
      return (xs.Min() + xs.Max()) / 2;
    }

    // The person's ancestry hung above <paramref name="own"/>, which already holds the person and
    // whatever hangs below them. <paramref name="side"/> is where the person's siblings may go.
    private Shape Ancestry(int person, Side side, Shape own)
    {
      var key = _Families.KeyOf(person);
      if (key is null)
        return own;
      var unplaced = _Families
        .Partners(key)
        .Where(parent => !_Visited.Contains(parent))
        .OrderBy(parent => (_Families.Nodes[parent].Person.BiologicalSex != BiologicalSex.Male, BirthOrder(_Families.Nodes[parent])))
        .ToArray();
      var parents = Couples(unplaced);
      if (parents.Length == 0)
        return own;
      foreach (var parent in parents)
        _Visited.Add(parent);
      PlacedBy[person] = key;

      var sides = new Side[parents.Length];
      var units = new Shape[parents.Length];
      for (var i = 0; i < parents.Length; i++)
      {
        sides[i] = (i, parents.Length) switch
        {
          (_, 1) => side == Side.Left ? Side.Right : Side.Left,
          (0, _) => Side.Left,
          var (index, count) when index == count - 1 => Side.Right,
          _ => Side.Both,
        };
        var block = Block(parents[i], sides[i], key);
        units[i] = Ancestry(parents[i], sides[i], block);
      }

      var siblings = new List<(int Id, Shape Shape)>();
      foreach (var sibling in Ordered(_Families.ChildrenOf(key)))
      {
        if (sibling == person || !IsFree(sibling))
          continue;
        _Visited.Add(sibling);
        PlacedBy[sibling] = key;
        var subtree = Block(sibling, Side.Both, excludedKey: null);
        siblings.Add((sibling, subtree));
      }

      var sibship = Sibship(person, own, siblings, side);
      return Hang(sibship, person, parents, units, sides);
    }

    // Each parent followed by the spouses they have among the others, so birth and adoptive couples
    // each sit side by side.
    private int[] Couples(int[] parents)
    {
      var ordered = new List<int>();
      foreach (var parent in parents)
      {
        if (ordered.Contains(parent))
          continue;
        ordered.Add(parent);
        var spouses = parents.Where(p => !ordered.Contains(p) && _Families.AreSpouses(parent, p)).ToArray();
        ordered.AddRange(spouses);
      }
      return [.. ordered];
    }

    private Shape Sibship(int person, Shape own, List<(int Id, Shape Shape)> siblings, Side side)
    {
      (int Id, Shape Shape)[] ordered = side switch
      {
        Side.Left => [.. siblings, (person, own)],
        Side.Right => [(person, own), .. siblings],
        _ => [.. siblings.Append((Id: person, Shape: own)).OrderBy(s => (HintRank(s.Id), BirthOrder(_Families.Nodes[s.Id])))],
      };
      var shape = new Shape();
      foreach (var (_, subtree) in ordered)
        shape.Append(subtree);
      return shape;
    }

    // Hangs the sibship from its parents' units so the person sits under the parents' drop, between the
    // father's side and the mother's, as far as their own subtrees leave room.
    private static Shape Hang(Shape sibship, int person, int[] parents, Shape[] units, Side[] sides)
    {
      var xd = sibship.X[person];
      switch (parents.Length)
      {
        case 1:
          {
            var unit = units[0];
            var under = unit.X[parents[0]] - xd;
            var dx = sides[0] == Side.Right
              ? Math.Min(under, -sibship.ClearanceTo(unit))
              : Math.Max(under, unit.ClearanceTo(sibship));
            unit.Absorb(sibship, dx);
            return unit;
          }

        case 2:
          {
            var (left, right) = (units[0], units[1]);
            var a = left.X[parents[0]] + right.X[parents[1]];
            // The drop moves half as far as the mother's side, so each bound on the sibship is doubled.
            var r = Math.Max(
              left.ClearanceTo(right),
              Math.Max(
                (2 * (left.ClearanceTo(sibship) + xd)) - a,
                a - (2 * xd) + (2 * sibship.ClearanceTo(right))));
            left.Absorb(right, r);
            left.Absorb(sibship, ((a + r) / 2) - xd);
            return left;
          }

        default:
          {
            var all = units[0];
            foreach (var unit in units.Skip(1))
            {
              var dx = all.OffsetBeside(unit);
              all.Absorb(unit, dx);
            }
            var drop = (all.X[parents[0]] + all.X[parents[^1]]) / 2;
            var clearance = all.ClearanceTo(sibship);
            all.Absorb(sibship, Math.Max(drop - xd, clearance));
            return all;
          }
      }
    }
  }

  // ── pins ──────────────────────────────────────────────────────────────────────────────────────────

  private static Dictionary<int, double> ResolvePins(
    int centerId,
    Families families,
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, double>? pins)
  {
    var pinned = new Dictionary<int, double>();
    var applicable = pins?.Where(pin => x.ContainsKey(pin.Key)).ToArray() ?? [];
    if (applicable.Length == 0)
      return pinned;

    var centerX = x[centerId];
    pinned[centerId] = centerX;
    foreach (var (id, offset) in applicable)
    {
      var generation = families.Generation(id);
      var taken = pinned
        .Where(pin => families.Generation(pin.Key) == generation)
        .Select(pin => pin.Value)
        .ToArray();
      pinned[id] = NearestClearSlot(centerX + offset, taken);
    }
    return pinned;
  }

  // Moves the free nodes of each pinned row as little as keeps them in order and a slot clear of each
  // other and of the pins. A gap between two pins takes only as many nodes as fit; the rest move on.
  private static void ClearPins(Families families, Dictionary<int, double> x, IReadOnlyDictionary<int, double> pinned)
  {
    foreach (var generation in families.Nodes.Values.Select(node => node.Generation).Distinct())
    {
      var row = families.Row(generation);
      var pins = row.Where(pinned.ContainsKey).Select(id => x[id]).Order().ToArray();
      if (pins.Length == 0)
        continue;

      var free = row.Where(id => !pinned.ContainsKey(id)).OrderBy(id => x[id]).ThenBy(id => id).ToArray();
      var segments = Enumerable.Range(0, pins.Length + 1).Select(_ => new List<int>()).ToArray();
      int Capacity(int segment) => segment == 0 || segment == pins.Length
        ? int.MaxValue
        : (int)Math.Floor(pins[segment] - pins[segment - 1] + SlotTolerance) - 1;

      var current = 0;
      foreach (var id in free)
      {
        var segment = Math.Max(current, pins.Count(pin => pin < x[id]));
        while (segments[segment].Count >= Capacity(segment))
          segment++;
        segments[segment].Add(id);
        current = segment;
      }

      for (var segment = 0; segment < segments.Length; segment++)
      {
        var members = segments[segment];
        if (members.Count == 0)
          continue;
        var low = segment == 0 ? double.NegativeInfinity : pins[segment - 1] + 1;
        var high = segment == pins.Length ? double.PositiveInfinity : pins[segment] - members.Count;
        // With z = x - index the order-and-gap constraint is just z non-decreasing, so the nearest such
        // row is the isotonic regression of the wanted positions, clamped to the room between the pins.
        var wanted = members.Select((id, index) => x[id] - index).ToArray();
        var z = Isotonic(wanted);
        for (var index = 0; index < members.Count; index++)
          x[members[index]] = Math.Clamp(z[index], low, high) + index;
      }
    }
  }

  // Pool-adjacent-violators: the non-decreasing sequence nearest to the values in least squares.
  private static double[] Isotonic(double[] values)
  {
    var means = new List<double>();
    var counts = new List<int>();
    foreach (var value in values)
    {
      means.Add(value);
      counts.Add(1);
      while (means.Count > 1 && means[^2] > means[^1])
      {
        var count = counts[^2] + counts[^1];
        var mean = ((means[^2] * counts[^2]) + (means[^1] * counts[^1])) / count;
        means.RemoveAt(means.Count - 1);
        counts.RemoveAt(counts.Count - 1);
        means[^1] = mean;
        counts[^1] = count;
      }
    }
    return [.. means.SelectMany((mean, block) => Enumerable.Repeat(mean, counts[block]))];
  }

  // ── drawing ───────────────────────────────────────────────────────────────────────────────────────

  // A horizontal run in the band below a row, waiting for the track it is drawn on.
  private sealed class Run(int band, double from, double to)
  {
    public int Band { get; } = band;
    public double Left { get; } = Math.Min(from, to);
    public double Right { get; } = Math.Max(from, to);
    public double Y { get; set; }
  }

  private readonly record struct Waypoint(double X, double Y, Run? Track = null);

  private sealed class Drawing
  {
    private readonly List<(FamilyTreeRelation Relation, bool IsLoop, Waypoint[] Points)> _Lines = [];
    private readonly List<Run> _Runs = [];

    public Run Track(int band, double from, double to)
    {
      var run = new Run(band, from, to);
      _Runs.Add(run);
      return run;
    }

    public void Line(FamilyTreeRelation relation, bool isLoop, params Waypoint[] points) =>
      _Lines.Add((relation, isLoop, points));

    // Left-edge channel routing: runs that overlap, or come within half a gap of each other, in one band
    // take separate tracks spread evenly through the band.
    public FamilyTreeConnector[] Resolve(Func<int, double> bandTop, FamilyTreeLayoutMetrics metrics)
    {
      foreach (var band in _Runs.GroupBy(run => run.Band))
      {
        var tracks = new List<double>();
        var assigned = new List<(Run Run, int Track)>();
        foreach (var run in band.OrderBy(run => run.Left).ThenBy(run => run.Right))
        {
          var track = tracks.FindIndex(right => run.Left >= right + (metrics.HorizontalGap / 2));
          if (track < 0)
          {
            track = tracks.Count;
            tracks.Add(run.Right);
          }
          tracks[track] = run.Right;
          assigned.Add((run, track));
        }
        var top = bandTop(band.Key);
        foreach (var (run, track) in assigned)
          run.Y = Math.Round(top + ((track + 1) * metrics.VerticalGap / (tracks.Count + 1)));
      }

      return [.. _Lines.Select(line => new FamilyTreeConnector(line.Relation, Simplify(line.Points), line.IsLoop))];
    }

    // Drops repeated points and the middle of three in a line, so every point left is a real bend.
    private static PointF[] Simplify(Waypoint[] waypoints)
    {
      var points = new List<PointF>();
      foreach (var waypoint in waypoints)
      {
        var point = new PointF((float)waypoint.X, (float)(waypoint.Track?.Y ?? waypoint.Y));
        if (points.Count != 0 && points[^1] == point)
          continue;
        if (points.Count >= 2)
        {
          var (a, b) = (points[^2], points[^1]);
          var straight = (a.X == b.X && b.X == point.X) || (a.Y == b.Y && b.Y == point.Y);
          if (straight)
            points.RemoveAt(points.Count - 1);
        }
        points.Add(point);
      }
      return [.. points];
    }
  }

  private static FamilyTreeLayoutResult Assemble(
    int centerId,
    Families families,
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, string> placedBy,
    FamilyTreeLayoutMetrics metrics)
  {
    var maxGeneration = families.Nodes.Values.Max(node => node.Generation);
    var minSlot = x.Values.Min();
    var origin = Math.Ceiling(metrics.Margin + (metrics.NodeWidth / 2));

    // The one place a slot becomes a pixel, so positions equal in slots stay equal on screen.
    double Snap(double slot) => origin + Math.Round((slot - minSlot) * metrics.SlotPitch, MidpointRounding.AwayFromZero);
    double CentreX(int id) => Snap(x[id]);
    double Top(int generation) => metrics.Margin + ((maxGeneration - generation) * metrics.RowPitch);
    double Bottom(int generation) => Top(generation) + metrics.NodeHeight;
    double Middle(int generation) => Top(generation) + (metrics.NodeHeight / 2);

    var layouts = new List<FamilyTreeNodeLayout>(families.Nodes.Count);
    foreach (var node in families.Nodes.Values)
    {
      var left = CentreX(node.Id) - (metrics.NodeWidth / 2);
      var rect = new Rect(left, Top(node.Generation), metrics.NodeWidth, metrics.NodeHeight);
      layouts.Add(new FamilyTreeNodeLayout(node, rect));
    }

    var drawing = new Drawing();
    var attachments = new Dictionary<int, List<double>>();
    var gapUses = new Dictionary<(int Generation, double Column), int>();
    var hosts = new HashSet<int>();

    // A loop leaves a node off its centre line, which belongs to the node's own families, and off any
    // column another loop already runs down in the same band, as from a node straight above or below.
    double Attach(int id, bool top, double toward)
    {
      var band = families.Generation(id) + (top ? 1 : 0);
      if (!attachments.TryGetValue(band, out var taken))
        attachments[band] = taken = [];
      var centre = CentreX(id);
      var sign = toward >= centre ? 1 : -1;
      var columns = new[] { 0.25, -0.25, 0.375, -0.375, 0.125, -0.125 }
        .Select(reach => Math.Round(centre + (sign * reach * metrics.NodeWidth)))
        .ToArray();
      var column = columns.FirstOrDefault(c => !taken.Any(t => Math.Abs(t - c) < 1), columns[0]);
      taken.Add(column);
      return column;
    }

    // Where a vertical can cross a row: in a gap between two of its nodes, or past either end.
    double Column(int generation, double from, double to)
    {
      var centres = families.Row(generation).Select(CentreX).Order().ToArray();
      if (centres.Length == 0)
        return from;
      var half = metrics.SlotPitch / 2;
      var candidates = centres
        .Zip(centres.Skip(1), (a, b) => (a + b) / 2)
        .Prepend(centres[0] - half)
        .Append(centres[^1] + half);
      var gap = candidates.MinBy(c => (Math.Abs(c - from) + Math.Abs(c - to), c));
      var uses = gapUses.GetValueOrDefault((generation, gap));
      gapUses[(generation, gap)] = uses + 1;
      // Off the gap's centre line, where a couple's drop may already run.
      var offset = (uses % 2 == 0 ? 1 : -1) * metrics.HorizontalGap / 4;
      return Math.Round(gap + offset);
    }

    // Over the row through the band above it, or below the top row through the band under it.
    void Bridge(int a, int b, FamilyTreeRelation relation, bool isLoop)
    {
      var generation = families.Generation(a);
      var above = generation < maxGeneration;
      var y = above ? Top(generation) : Bottom(generation);
      var band = above ? generation + 1 : generation;
      var bx = CentreX(b);
      var ax = Attach(a, above, bx);
      var ex = Attach(b, above, CentreX(a));
      var run = drawing.Track(band, ax, ex);
      drawing.Line(relation, isLoop, new(ax, y), new(ax, 0, run), new(ex, 0, run), new(ex, y));
    }

    void Loop(int a, int b, FamilyTreeRelation relation)
    {
      if (families.Generation(a) == families.Generation(b))
      {
        Bridge(a, b, relation, isLoop: true);
        return;
      }

      var (upper, lower) = families.Generation(a) > families.Generation(b) ? (a, b) : (b, a);
      var upperGeneration = families.Generation(upper);
      var lowerGeneration = families.Generation(lower);
      var start = Attach(upper, top: false, CentreX(lower));
      var end = Attach(lower, top: true, CentreX(upper));
      var points = new List<Waypoint> { new(start, Bottom(upperGeneration)) };
      var current = start;
      for (var generation = upperGeneration - 1; generation >= lowerGeneration; generation--)
      {
        var next = generation == lowerGeneration ? end : Column(generation, current, end);
        if (Math.Abs(next - current) >= 0.5)
        {
          var run = drawing.Track(generation + 1, current, next);
          points.Add(new(current, 0, run));
          points.Add(new(next, 0, run));
        }
        current = next;
      }
      points.Add(new(end, Top(lowerGeneration)));
      drawing.Line(relation, true, [.. points]);
    }

    foreach (var key in families.Keys)
    {
      var partners = families.Partners(key).OrderBy(p => x[p]).ToArray();
      var children = families.ChildrenOf(key);
      var placed = children.Where(child => placedBy.GetValueOrDefault(child) == key).OrderBy(child => x[child]).ToArray();
      var generation = families.Generation(partners[0]);

      if (placed.Length != 0)
      {
        var together = partners.Length > 1 && IsClear(families, x, partners[0], partners[^1], partners);
        var married = partners.Zip(partners.Skip(1)).All(pair => families.AreSpouses(pair.First, pair.Second));
        (double X, double Top)[] drops;
        if (together && married)
        {
          var drop = (x[partners[0]] + x[partners[^1]]) / 2;
          var dropX = Snap(drop);
          var onPartner = partners.Any(p => Math.Abs(CentreX(p) - dropX) < metrics.NodeWidth / 2);
          drops = [(dropX, onPartner ? Bottom(generation) : Middle(generation))];
        }
        else
        {
          var mean = placed.Average(child => x[child]);
          var host = partners.MinBy(p => (Math.Abs(x[p] - mean), x[p]));
          // Parents with no marriage on record, as the GEDCOM import leaves a family without a MARR, each
          // drop to the bar: a line between them would claim the marriage.
          var droppers = together ? partners : [host];
          var toward = Snap(mean);
          var bottom = Bottom(generation);
          // A second family hung from one person, as after a third marriage, drops beside the first.
          drops = [.. droppers.Select(p => (hosts.Add(p) ? CentreX(p) : Attach(p, top: false, toward), bottom))];
          foreach (var partner in partners.Where(p => !droppers.Contains(p) && !families.AreSpouses(p, host)))
            Loop(partner, host, FamilyTreeRelation.ParentChild);
        }

        var childTop = Top(generation - 1);
        var xs = placed.Select(CentreX).ToArray();
        var tops = drops.ToDictionary(d => d.X, d => d.Top);
        var columns = tops.Keys.Union(xs).Order().ToArray();
        bool Through(double column) => tops.ContainsKey(column) && xs.Contains(column);

        foreach (var column in columns.Where(Through))
          drawing.Line(FamilyTreeRelation.ParentChild, false, new(column, tops[column]), new(column, childTop));
        if (columns.Length > 1)
        {
          var (low, high) = (columns[0], columns[^1]);
          var bar = drawing.Track(generation, low, high);
          Waypoint End(double column) =>
            Through(column) ? new(column, 0, bar) : new(column, tops.GetValueOrDefault(column, childTop));

          // The bar turns into its two end columns round a corner; every column between meets it at a T.
          drawing.Line(FamilyTreeRelation.ParentChild, false, End(low), new(low, 0, bar), new(high, 0, bar), End(high));
          foreach (var column in columns[1..^1].Where(column => !Through(column)))
            drawing.Line(FamilyTreeRelation.ParentChild, false, new(column, 0, bar), End(column));
        }
      }

      foreach (var child in children.Except(placed))
      {
        var nearest = partners.MinBy(p => (Math.Abs(x[p] - x[child]), x[p]));
        Loop(nearest, child, FamilyTreeRelation.ParentChild);
      }
    }

    foreach (var (a, b) in families.Spouses)
    {
      if (families.Generation(a) != families.Generation(b))
      {
        Loop(a, b, FamilyTreeRelation.Spouse);
        continue;
      }
      if (!IsClear(families, x, a, b, [a, b]))
      {
        Bridge(a, b, FamilyTreeRelation.Spouse, isLoop: false);
        continue;
      }
      // Centre to centre: the photos cover the ends, so the line reaches each photo however much wider
      // than the photo the node is.
      var (left, right) = x[a] <= x[b] ? (a, b) : (b, a);
      var y = Middle(families.Generation(a));
      drawing.Line(FamilyTreeRelation.Spouse, false, new(CentreX(left), y), new(CentreX(right), y));
    }

    foreach (var (parent, child) in families.OffRowParents)
      Loop(parent, child, FamilyTreeRelation.ParentChild);

    var connectors = drawing.Resolve(Bottom, metrics);
    var points = connectors.SelectMany(connector => connector.Points).ToArray();
    var width = layouts.Max(l => l.Bounds.Right);
    var height = layouts.Max(l => l.Bounds.Bottom);
    if (points.Length != 0)
    {
      width = Math.Max(width, points.Max(p => p.X));
      height = Math.Max(height, points.Max(p => p.Y));
    }
    var centerTopLeft = layouts.Single(l => l.Node.Id == centerId).Bounds.Location;

    return new FamilyTreeLayoutResult(
      layouts,
      connectors,
      new Size(width + metrics.Margin, height + metrics.Margin),
      centerTopLeft,
      metrics);
  }
}
