using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

// The tree's families: a child belongs to the family of the parents it has in the row above. A parent
// in any other row, and a spouse in another row, is a relationship the rows cannot hold.
internal sealed class Families
{
  private readonly Dictionary<int, int[]> _ParentsOf;
  private readonly Dictionary<string, int[]> _Partners;
  private readonly Dictionary<string, int[]> _Children;
  private readonly Dictionary<int, string[]> _KeysOf;
  private readonly Dictionary<int, int[]> _SpousesOf;
  private readonly Dictionary<int, int[]> _Rows;
  private readonly Dictionary<int, int[]> _ChildrenByParent;

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
    var childrenByKey = _ParentsOf.GroupBy(pair => Key(pair.Value));
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
    _ChildrenByParent = parentEdges
      .GroupBy(edge => edge.FromId)
      .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToId).ToArray());
  }

  public IReadOnlyDictionary<int, FamilyTreeNode> Nodes { get; }

  public string[] Keys { get; }

  public (int Parent, int Child)[] OffRowParents { get; }

  public (int A, int B)[] Spouses { get; }

  public static string Key(IEnumerable<int> parents) => string.Join(",", parents.Order());

  public int Generation(int id) => Nodes[id].Generation;

  public int[] ParentsOf(int child) => _ParentsOf.GetValueOrDefault(child, []);

  public string? KeyOf(int child) => _ParentsOf.TryGetValue(child, out var parents) ? Key(parents) : null;

  public int[] Partners(string key) => _Partners[key];

  public int[] ChildrenOf(string key) => _Children[key];

  public string[] KeysOf(int partner) => _KeysOf.GetValueOrDefault(partner, []);

  public int[] SpousesOf(int id) => _SpousesOf.GetValueOrDefault(id, []);

  public int[] Row(int generation) => _Rows.GetValueOrDefault(generation, []);

  public bool AreSpouses(int a, int b) => SpousesOf(a).Contains(b);

  // In any row, so a parent the rows cannot hold still counts.
  public int[] ChildrenOfBoth(int a, int b)
  {
    var childrenOfA = _ChildrenByParent.GetValueOrDefault(a, []);
    var childrenOfB = _ChildrenByParent.GetValueOrDefault(b, []);
    return [.. childrenOfA.Intersect(childrenOfB)];
  }

  public bool IsClearBetween(IReadOnlyDictionary<int, double> x, int a, int b, int[] except)
  {
    var low = Math.Min(x[a], x[b]);
    var high = Math.Max(x[a], x[b]);
    var row = Row(Generation(a));
    return !row.Any(id => !except.Contains(id) && x[id] > low + FamilyTreeLayout.SlotTolerance && x[id] < high - FamilyTreeLayout.SlotTolerance);
  }
}
