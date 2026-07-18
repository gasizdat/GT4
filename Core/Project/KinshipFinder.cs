using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

/// <summary>
/// Finds the kinship chain between two persons by walking <see cref="RelativesProvider"/>'s typed
/// expansion breadth-first from <c>source</c> until <c>target</c> is reached. Searching in the typed
/// space (rather than the raw parent/child/spouse graph) guarantees every node on the returned chain
/// already carries a nameable relationship to <c>source</c> -- the same one the UI's relatives list
/// renders elsewhere. The cost is that a target reachable only through a spouse's blood family beyond
/// their parents (e.g. a spouse's sibling) is reported as unrelated: <see cref="RelativesProvider"/>'s
/// in-law expansion does not reach that far.
/// </summary>
internal sealed class KinshipFinder : ProjectComponentBase, IKinshipFinder
{
  internal KinshipFinder(IProjectDocument document)
    : base(document)
  {
  }

  // Mirrors PersonPage.AssembleRoots: the subject's own siblings, in-laws and step-relations are not
  // part of RelativesProvider's raw Person-rooted expansion (that only surfaces direct parent/child/
  // spouse links) -- they are derived separately via GetParentsAsync/GetSiblings/GetStepChildrenAsync,
  // the same way the app's relatives panel seeds its root row.
  private async Task<RelativeInfo[]> GetRootsAsync(Person person, CancellationToken token)
  {
    var relativesProvider = Document.RelativesProvider;
    var personFullInfo = await Document.PersonManager.GetPersonFullInfoAsync(person, token);
    var parentsTask = relativesProvider.GetParentsAsync(personFullInfo.RelativeInfos, token);
    var stepChildrenTask = relativesProvider.GetStepChildrenAsync(personFullInfo.RelativeInfos, token);
    await Task.WhenAll(parentsTask, stepChildrenTask);
    var parents = parentsTask.Result;
    var siblings = relativesProvider.GetSiblings(personFullInfo, parents);

    return
    [
      .. personFullInfo.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse),
      .. parents.Native,
      .. parents.Adoptive,
      .. parents.Step,
      .. siblings.Native,
      .. siblings.ByFather,
      .. siblings.ByMother,
      .. siblings.Step,
      .. siblings.Adoptive,
      .. relativesProvider.GetChildren(personFullInfo.RelativeInfos),
      .. relativesProvider.GetAdoptiveChildren(personFullInfo.RelativeInfos),
      .. stepChildrenTask.Result
    ];
  }

  public async Task<RelativeInfo[]?> FindPathAsync(Person source, Person target, CancellationToken token)
  {
    var roots = await GetRootsAsync(source, token);
    var frontier = new Queue<RelativeInfo[]>(roots.Select(root => new[] { root }));
    var visited = new HashSet<int>();

    while (frontier.Count > 0)
    {
      var path = frontier.Dequeue();
      var current = path[^1];
      if (current.Id == target.Id)
      {
        return path;
      }

      if (!visited.Add(current.Id))
      {
        continue;
      }

      var children = await Document.RelativesProvider.GetRelativeInfosAsync(current, selectMainPhoto: true, token);
      foreach (var child in children.Where(child => !visited.Contains(child.Id)))
      {
        frontier.Enqueue([.. path, child]);
      }
    }

    return null;
  }
}
