using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal class RelativesProvider : TableBase, IRelativesProvider
{
  private static readonly ElementIdComparer<RelativeInfo> _RelativeInfoComparer = new();

  private async Task<RelativeFullInfo> GetRelativeFullInfoAsync(RelativeInfo relative, CancellationToken token) =>
    new(relative: relative, relativeInfos: await GetRelativeInfosAsync(relative, true, token));

  private static RelativeInfo[] ToTypedArray(IEnumerable<RelativeInfo> sibling, RelationshipType type) =>
    [.. sibling.Select(s => s with { Type = type })];

  internal RelativesProvider(IProjectDocument document)
    : base(document)
  {

  }

  internal override Task CreateAsync(CancellationToken token)
  {
    throw new NotSupportedException();
  }

  public async Task<RelativeInfo[]> GetRelativeInfosAsync(Person person, bool selectMainPhoto, CancellationToken token)
  {
    var relatives = await Document.Relatives.GetRelativesAsync(person, token);
    var personInfos = await Document.PersonManager.GetPersonInfosAsync(
      persons: relatives,
      selectMainPhoto: selectMainPhoto,
      token: token);

    var relativeInfos = new RelativeInfo[personInfos.Length];
    for (var i = 0; i < personInfos.Length; i++)
    {
      var relative = relatives[i];
      var personInfo = personInfos[i];
      relativeInfos[i] = new RelativeInfo(relative, personInfo.Names, personInfo.MainPhoto);
    }

    return relativeInfos;
  }

  public async Task<Parents> GetParentsAsync(RelativeInfo[] relativeInfos, CancellationToken token)
  {
    var parentTasks = relativeInfos
      .Where(r => r.Type == RelationshipType.Parent)
      .Select(r => GetRelativeFullInfoAsync(r, token));
    var adoptiveParentTasks = relativeInfos
      .Where(r => r.Type == RelationshipType.AdoptiveParent)
      .Select(r => GetRelativeFullInfoAsync(r, token));
    var parentsTasks = Task.WhenAll(parentTasks);
    var adoptiveParentsTasks = Task.WhenAll(adoptiveParentTasks);
    await Task.WhenAll(parentsTasks, adoptiveParentsTasks);
    var parents = parentsTasks.Result;
    var adoptiveParents = adoptiveParentsTasks.Result;
    var allParentIds = new HashSet<int>([.. parents.Select(p => p.Id), .. adoptiveParents.Select(p => p.Id)]);
    var stepParentTasks = parents
      .SelectMany(p => p.RelativeInfos)
      .Where(r => r.Type == RelationshipType.Spose)
      .Where(r => !allParentIds.Contains(r.Id))
      .Select(r => GetRelativeFullInfoAsync(r with { Type = RelationshipType.StepParent }, token));
    var stepParents = await Task.WhenAll(stepParentTasks);

    return new Parents(
      Native: parents,
      Adoptive: adoptiveParents,
      Step: stepParents);
  }

  public async Task<RelativeInfo[]> GetStepChildrenAsync(RelativeInfo[] relativeInfos, CancellationToken token)
  {
    var ownChildrenIds = relativeInfos
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Select(r => r.Id)
      .ToHashSet();
    var sposesTasks = relativeInfos
      .Where(r => r.Type == RelationshipType.Spose)
      .Select(r => GetRelativeFullInfoAsync(r, token));
    var sposes = await Task.WhenAll(sposesTasks);
    var ret = sposes
      .SelectMany(s => s.RelativeInfos.Select(r => r with { Date = s.Date }))
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Where(r => !ownChildrenIds.Contains(r.Id));

    return ToTypedArray(ret, RelationshipType.StepChild);
  }

  public Siblings GetSiblings(Person person, Parents parents)
  {
    var allMotherChildren = parents.Native
        .Where(p => p.BiologicalSex == BiologicalSex.Female)
        .SelectMany(p => p.RelativeInfos)
        .Where(r => r.Type == RelationshipType.Child && r.Id != person.Id)
        .ToArray();
    var allFatherChildren = parents.Native
        .Where(p => p.BiologicalSex == BiologicalSex.Male)
        .SelectMany(p => p.RelativeInfos)
        .Where(r => r.Type == RelationshipType.Child && r.Id != person.Id)
        .ToArray();
    var commonChildren = allFatherChildren.Intersect(allMotherChildren, _RelativeInfoComparer);
    var fatherChildren = allFatherChildren.Except(commonChildren, _RelativeInfoComparer);
    var motherChildren = allMotherChildren.Except(commonChildren, _RelativeInfoComparer);
    var adoptiveChildrenOfNativeParents = parents.Native
      .SelectMany(p => p.RelativeInfos)
      .Where(r => r.Type == RelationshipType.AdoptiveChild);
    var childrenOfAdoptiveParents = parents.Adoptive
        .SelectMany(p => p.RelativeInfos.Select(r => r with { Date = p.Date }))
        .Where(r => r.Type == RelationshipType.Child || (r.Type == RelationshipType.AdoptiveChild && r.Id != person.Id));
    var adoptiveChildren = ((IEnumerable<RelativeInfo>)[.. adoptiveChildrenOfNativeParents, .. childrenOfAdoptiveParents])
      .Distinct(_RelativeInfoComparer);
    var stepParentChildren = parents.Step
      .SelectMany(p => p.RelativeInfos.Select(r => r with { Date = p.Date }))
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Distinct(_RelativeInfoComparer);

    return new Siblings(
      Native: ToTypedArray(commonChildren, RelationshipType.Sibling),
      ByMother: ToTypedArray(motherChildren, RelationshipType.SiblingByMother),
      ByFather: ToTypedArray(fatherChildren, RelationshipType.SiblingByFather),
      Adoptive: ToTypedArray(adoptiveChildren, RelationshipType.AdoptiveSibling),
      Step: ToTypedArray(stepParentChildren, RelationshipType.StepSibling));
  }

  public RelativeInfo[] Children(RelativeInfo[] relativeInfos) =>
    relativeInfos
    .Where(r => r.Type == RelationshipType.Child)
    .ToArray();

  public RelativeInfo[] AdoptiveChildren(RelativeInfo[] relativeInfos) =>
    relativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveChild)
    .ToArray();
}
