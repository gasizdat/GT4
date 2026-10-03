using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Extensions;

public static class RelativesProviderExtensions
{
  // The top level of a person's relatives tree, before any relative is expanded.
  public static async Task<RelativeInfo[]> GetRootsAsync(
    this IRelativesProvider relativesProvider,
    PersonFullInfo person,
    CancellationToken token)
  {
    var parents = await relativesProvider.GetParentsAsync(person.RelativeInfos, token);
    var stepChildren = await relativesProvider.GetStepChildrenAsync(person.RelativeInfos, token);
    var siblings = relativesProvider.GetSiblings(person, parents);
    var roots = new List<RelativeInfo>();
    void Add(IEnumerable<RelativeInfo> relatives) => roots.AddRange(relatives.OrderBy(r => r.BiologicalSex));

    Add(person.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse));
    Add(parents.Native);
    Add(parents.Adoptive);
    Add(parents.Step);
    Add(siblings.Native);
    Add(siblings.ByFather);
    Add(siblings.ByMother);
    Add(siblings.Step);
    Add(siblings.Adoptive);
    Add(relativesProvider.GetChildren(person.RelativeInfos));
    Add(relativesProvider.GetAdoptiveChildren(person.RelativeInfos));
    Add(stepChildren);

    return [.. roots];
  }
}
