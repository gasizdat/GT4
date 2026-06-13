using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

/// <summary>
/// Assembles the <see cref="FamilyTree"/> shown by the family-tree view. Unlike
/// <see cref="RelativesProvider"/>, which reinterprets relationships into kinship terms for the
/// relatives list, this provider walks the raw parent/child/spouse links so the graph mirrors the
/// stored pedigree directly.
/// </summary>
internal sealed class FamilyTreeProvider : TableBase, IFamilyTreeProvider
{
  internal FamilyTreeProvider(IProjectDocument document)
    : base(document)
  {
  }

  public async Task<FamilyTree> BuildAsync(
    Person center,
    int ancestorGenerations,
    int descendantGenerations,
    CancellationToken token)
  {
    ArgumentNullException.ThrowIfNull(center);

    var nodes = new Dictionary<int, FamilyTreeNode>();
    var edges = new HashSet<FamilyTreeEdge>();

    var centerInfo = await GetPersonInfoAsync(center, token);
    var centerNode = new FamilyTreeNode(centerInfo, Generation: 0);
    nodes[center.Id] = centerNode;

    // Ancestors: starting at the centre, climb to parents one generation at a time.
    await ExpandAsync(
      seed: centerNode,
      depth: ancestorGenerations,
      generationStep: +1,
      follow: static type => type is RelationshipType.Parent or RelationshipType.AdoptiveParent,
      makeEdge: static (fromId, relativeId) => FamilyTreeEdge.ParentChild(parentId: relativeId, childId: fromId),
      nodes: nodes,
      edges: edges,
      token: token);

    // Descendants: starting at the centre, descend to children one generation at a time.
    await ExpandAsync(
      seed: centerNode,
      depth: descendantGenerations,
      generationStep: -1,
      follow: static type => type is RelationshipType.Child or RelationshipType.AdoptiveChild,
      makeEdge: static (fromId, relativeId) => FamilyTreeEdge.ParentChild(parentId: fromId, childId: relativeId),
      nodes: nodes,
      edges: edges,
      token: token);

    await AddSpousesAsync(nodes, edges, token);

    return new FamilyTree(center.Id, [.. nodes.Values], [.. edges]);
  }

  private async Task ExpandAsync(
    FamilyTreeNode seed,
    int depth,
    int generationStep,
    Func<RelationshipType, bool> follow,
    Func<int, int, FamilyTreeEdge> makeEdge,
    Dictionary<int, FamilyTreeNode> nodes,
    HashSet<FamilyTreeEdge> edges,
    CancellationToken token)
  {
    var frontier = new List<FamilyTreeNode> { seed };

    for (var level = 0; level < depth && frontier.Count != 0; level++)
    {
      var next = new List<FamilyTreeNode>();

      foreach (var node in frontier)
      {
        var generation = node.Generation + generationStep;
        var matches = await GetRelativesAsync(node.Person, follow, token);

        foreach (var relative in matches)
        {
          edges.Add(makeEdge(node.Person.Id, relative.Id));

          if (nodes.ContainsKey(relative.Id))
          {
            continue;
          }

          var added = new FamilyTreeNode(relative, generation);
          nodes[relative.Id] = added;
          next.Add(added);
        }
      }

      frontier = next;
    }
  }

  private async Task AddSpousesAsync(
    Dictionary<int, FamilyTreeNode> nodes,
    HashSet<FamilyTreeEdge> edges,
    CancellationToken token)
  {
    // Spouses sit on the same generation as the person they marry into. Snapshot the blood relatives
    // first so the spouses added here are not themselves expanded for further spouses.
    foreach (var node in nodes.Values.ToList())
    {
      var spouses = await GetRelativesAsync(node.Person, static type => type == RelationshipType.Spouse, token);

      foreach (var spouse in spouses)
      {
        edges.Add(FamilyTreeEdge.Spouse(node.Person.Id, spouse.Id));

        if (!nodes.ContainsKey(spouse.Id))
        {
          nodes[spouse.Id] = new FamilyTreeNode(spouse, node.Generation);
        }
      }
    }
  }

  private async Task<PersonInfo[]> GetRelativesAsync(
    Person person,
    Func<RelationshipType, bool> follow,
    CancellationToken token)
  {
    var relatives = await Document.Relatives.GetRelativesAsync(person, token);
    var matched = relatives.Where(relative => follow(relative.Type)).ToArray();

    if (matched.Length == 0)
    {
      return [];
    }

    return await Document.PersonManager.GetPersonInfosAsync(matched, selectMainPhoto: true, token);
  }

  private async Task<PersonInfo> GetPersonInfoAsync(Person person, CancellationToken token)
  {
    var infos = await Document.PersonManager.GetPersonInfosAsync([person], selectMainPhoto: true, token);
    return infos.Single();
  }

  internal override Task CreateAsync(CancellationToken token) => throw new NotSupportedException();
}
