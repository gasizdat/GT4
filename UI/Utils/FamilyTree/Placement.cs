using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI.Utils.Genealogy;

internal sealed class Placement
{
  private readonly Families _Families;
  private readonly IReadOnlyDictionary<int, Hint> _Hints;
  private readonly HashSet<int> _Visited = [];
  // The centre and its ancestors: only the ancestry walk places them, never a collateral's subtree.
  private readonly HashSet<int> _DirectLine = [];

  private Placement(Families families, IReadOnlyDictionary<int, Hint> hints)
  {
    _Families = families;
    _Hints = hints;
  }

  public Dictionary<int, double> X { get; private set; } = [];

  // The family whose sibship placed each child. A child placed elsewhere hangs from its family by a loop.
  public Dictionary<int, string> PlacedBy { get; } = [];

  public static Placement Compute(Families families, int centerId)
  {
    var placement = new Placement(families, new Dictionary<int, Hint>());
    placement.Run(centerId);

    // kinship2's autohint: married blood relatives in one row move to the facing ends of their sibships.
    var hints = FacingEnds(families, placement.X);
    if (hints.Count == 0)
      return placement;
    placement = new Placement(families, hints);
    placement.Run(centerId);
    return placement;
  }

  private enum Side { Left, Right, Both }

  // Which end of their sibship a person should take to face the spouse they married from another one.
  private readonly record struct Hint(int Direction, int Partner);

  private static (bool, int, int) BirthOrder(FamilyTreeNode node) =>
    (node.Person.BirthDate.Status == DateStatus.Unknown, node.Person.BirthDate.Code, node.Id);

  private static Dictionary<int, Hint> FacingEnds(Families families, IReadOnlyDictionary<int, double> x)
  {
    var hints = new Dictionary<int, Hint>();
    foreach (var (a, b) in families.Spouses)
    {
      var blood = families.ParentsOf(a).Length != 0 && families.ParentsOf(b).Length != 0;
      if (!blood || families.Generation(a) != families.Generation(b) || families.IsClear(x, a, b, [a, b]))
        continue;
      var direction = Math.Sign(x[b] - x[a]);
      hints.TryAdd(a, new Hint(direction, b));
      hints.TryAdd(b, new Hint(-direction, a));
    }
    return hints;
  }

  private void Run(int centerId)
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
    var contour = Ancestry(centerId, Side.Both, own);

    var rest = _Families.Nodes.Values.OrderByDescending(node => node.Generation).ThenBy(node => node.Id);
    foreach (var node in rest)
    {
      if (!_Visited.Add(node.Id))
        continue;
      var part = Block(node.Id, Side.Both, excludedKey: null);
      var dx = contour.OffsetBeside(part);
      contour.Absorb(part, dx);
    }

    X = contour.X;
  }

  private bool IsFree(int id) => !_Visited.Contains(id) && !_DirectLine.Contains(id);

  private int HintRank(int id) => _Hints.TryGetValue(id, out var hint) ? hint.Direction + 1 : 1;

  private IEnumerable<int> Ordered(IEnumerable<int> ids) =>
    ids.OrderBy(id => (HintRank(id), BirthOrder(_Families.Nodes[id])));

  // The person with the partners who join them on their row, and below them each family's children.
  // A partner on either side of the person is spread out until their family's drop is over its children.
  private Contour Block(int person, Side side, string? excludedKey)
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
        return Families.Key([person, sidePartners[0]]);
      return hint.Direction == direction ? Families.Key([person, hint.Partner]) : null;
    }
    var leftKey = SideKey(left, -1);
    var rightKey = SideKey(right, +1);

    var leftGroup = Group(keys.Where(key => key == leftKey));
    var middleGroup = Group(keys.Where(key => key != leftKey && key != rightKey));
    var rightGroup = Group(keys.Where(key => key == rightKey));

    var contour = new Contour();
    double? leftCentre = null;
    double? middleCentre = null;
    double? rightCentre = null;
    if (leftGroup is not null)
    {
      contour.Append(leftGroup.Value.Contour);
      leftCentre = Centre(contour, leftGroup.Value.Children);
    }
    if (middleGroup is not null)
    {
      contour.Append(middleGroup.Value.Contour);
      middleCentre = Centre(contour, middleGroup.Value.Children);
    }
    if (rightGroup is not null)
    {
      contour.Append(rightGroup.Value.Contour);
      rightCentre = Centre(contour, rightGroup.Value.Children);
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
    contour.Place(person, generation, x);

    var leftX = leftCentre is double lc && left.Count != 0 ? (2 * lc) - x : x - 1;
    for (var i = 0; i < left.Count; i++)
      contour.Place(left[i], generation, leftX - i);
    var rightX = rightCentre is double rc && right.Count != 0 ? (2 * rc) - x : x + 1;
    for (var i = 0; i < right.Count; i++)
      contour.Place(right[i], generation, rightX + i);

    return contour;
  }

  private (Contour Contour, int[] Children)? Group(IEnumerable<string> keys)
  {
    var contour = new Contour();
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
        contour.Append(subtree);
      }
    }
    return children.Count == 0 ? null : (contour, [.. children]);
  }

  private static double Centre(Contour contour, int[] children)
  {
    var xs = children.Select(child => contour.X[child]).ToArray();
    return (xs.Min() + xs.Max()) / 2;
  }

  // The person's ancestry hung above own, which already holds the person and whatever hangs below them.
  // side is where the person's siblings may go.
  private Contour Ancestry(int person, Side side, Contour own)
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
    var units = new Contour[parents.Length];
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

    var siblings = new List<(int Id, Contour Contour)>();
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

  private Contour Sibship(int person, Contour own, List<(int Id, Contour Contour)> siblings, Side side)
  {
    (int Id, Contour Contour)[] ordered = side switch
    {
      Side.Left => [.. siblings, (person, own)],
      Side.Right => [(person, own), .. siblings],
      _ => [.. siblings.Append((Id: person, Contour: own)).OrderBy(s => (HintRank(s.Id), BirthOrder(_Families.Nodes[s.Id])))],
    };
    var contour = new Contour();
    foreach (var (_, subtree) in ordered)
      contour.Append(subtree);
    return contour;
  }

  // Hangs the sibship from its parents' units so the person sits under the parents' drop, between the
  // father's side and the mother's, as far as their own subtrees leave room.
  private static Contour Hang(Contour sibship, int person, int[] parents, Contour[] units, Side[] sides)
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
