using GT4.Core.Project.Dto;

namespace GT4.UI.Components.Genealogy;

/// <summary>
/// Groups the people of a <see cref="FamilyTree"/> into <i>family units</i> for layout: a unit is a set
/// of same-generation people who are spouses or who share a child (co-parents). Treating a couple as one
/// block is what lets the tidy passes centre children under the couple's midpoint and keep partners side
/// by side, instead of placing the two parents as if they were unrelated.
/// <para>
/// Grouping is a union-find over the node ids: every spouse edge unions its two endpoints, and the
/// parents of each child are unioned together. Members of a unit are kept in a stable order so the unit
/// occupies a contiguous run of slots.
/// </para>
/// </summary>
internal sealed class FamilyUnits
{
  private readonly Dictionary<int, int> _Representative;
  private readonly Dictionary<int, List<int>> _Members;
  private readonly Dictionary<int, List<int>> _ChildUnits;
  private readonly Dictionary<int, List<int>> _ParentUnits;
  private readonly Dictionary<int, List<int>> _ParentsByChild;

  private FamilyUnits(
    Dictionary<int, int> representative,
    Dictionary<int, List<int>> members,
    Dictionary<int, List<int>> childUnits,
    Dictionary<int, List<int>> parentUnits,
    Dictionary<int, List<int>> parentsByChild)
  {
    _Representative = representative;
    _Members = members;
    _ChildUnits = childUnits;
    _ParentUnits = parentUnits;
    _ParentsByChild = parentsByChild;
  }

  /// <summary>The representative id of the unit a person belongs to.</summary>
  public int UnitOf(int personId) => _Representative[personId];

  /// <summary>The members of a unit, in a stable contiguous order.</summary>
  public IReadOnlyList<int> Members(int unit) => _Members[unit];

  /// <summary>The biological parents of a person that are present in the chart.</summary>
  public IReadOnlyList<int> ParentsOf(int personId) =>
    _ParentsByChild.TryGetValue(personId, out var parents) ? parents : [];

  /// <summary>Child units of a unit (units one generation down), in a deterministic order.</summary>
  public IReadOnlyList<int> ChildUnitsOf(int unit) =>
    _ChildUnits.TryGetValue(unit, out var children) ? children : [];

  /// <summary>Parent units of a unit (units one generation up), in a deterministic order.</summary>
  public IReadOnlyList<int> ParentUnitsOf(int unit) =>
    _ParentUnits.TryGetValue(unit, out var parents) ? parents : [];

  /// <summary>The centre slot of a unit given a slot assignment: members are contiguous, so the mean of the extremes.</summary>
  public double UnitCenter(int unit, IReadOnlyDictionary<int, double> x)
  {
    var members = _Members[unit];
    return (x[members[0]] + x[members[^1]]) / 2;
  }

  public static FamilyUnits Build(FamilyTree tree, IReadOnlyDictionary<int, FamilyTreeNode> nodesById)
  {
    var parent = nodesById.Keys.ToDictionary(id => id, id => id);

    int Find(int id)
    {
      while (parent[id] != id)
      {
        parent[id] = parent[parent[id]];
        id = parent[id];
      }

      return id;
    }

    void Union(int a, int b)
    {
      var ra = Find(a);
      var rb = Find(b);
      if (ra != rb)
      {
        parent[ra] = rb;
      }
    }

    // Spouses share a unit.
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation == FamilyTreeRelation.Spouse
        && nodesById.ContainsKey(edge.FromId)
        && nodesById.ContainsKey(edge.ToId))
      {
        Union(edge.FromId, edge.ToId);
      }
    }

    // Co-parents (everyone sharing a given child) share a unit, so unmarried co-parents still lay out
    // together and a child's drop comes from a single couple block.
    var parentsByChild = new Dictionary<int, List<int>>();
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.ParentChild
        || !nodesById.ContainsKey(edge.FromId)
        || !nodesById.ContainsKey(edge.ToId))
      {
        continue;
      }

      if (!parentsByChild.TryGetValue(edge.ToId, out var parents))
      {
        parentsByChild[edge.ToId] = parents = [];
      }

      parents.Add(edge.FromId);
    }

    foreach (var parents in parentsByChild.Values)
    {
      for (var i = 1; i < parents.Count; i++)
      {
        Union(parents[0], parents[i]);
      }
    }

    var representative = nodesById.Keys.ToDictionary(id => id, Find);

    // Members of each unit, ordered for a stable contiguous block.
    var members = representative
      .GroupBy(pair => pair.Value, pair => pair.Key)
      .ToDictionary(group => group.Key, group => group.OrderBy(id => id).ToList());

    // Adjacency at the unit level. Child/parent unit lists are ordered by the smallest member id so the
    // packing is deterministic.
    var childUnits = new Dictionary<int, HashSet<int>>();
    var parentUnits = new Dictionary<int, HashSet<int>>();
    foreach (var edge in tree.Edges)
    {
      if (edge.Relation != FamilyTreeRelation.ParentChild
        || !representative.TryGetValue(edge.FromId, out var parentUnit)
        || !representative.TryGetValue(edge.ToId, out var childUnit)
        || parentUnit == childUnit)
      {
        continue;
      }

      (childUnits.TryGetValue(parentUnit, out var kids) ? kids : childUnits[parentUnit] = []).Add(childUnit);
      (parentUnits.TryGetValue(childUnit, out var pars) ? pars : parentUnits[childUnit] = []).Add(parentUnit);
    }

    int MinMember(int unit) => members[unit][0];

    Dictionary<int, List<int>> Order(Dictionary<int, HashSet<int>> adjacency) =>
      adjacency.ToDictionary(pair => pair.Key, pair => pair.Value.OrderBy(MinMember).ToList());

    return new FamilyUnits(representative, members, Order(childUnits), Order(parentUnits), parentsByChild);
  }
}
