using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal class RelativesProvider : TableBase, IRelativesProvider
{
  private static readonly ElementIdComparer<RelativeInfo> _RelativeInfoComparer = new();

  private async Task<RelativeFullInfo> GetRelativeFullInfoAsync(RelativeInfo relative, CancellationToken token) =>
    new(relative: relative, relativeInfos: await GetRelativeInfosAsync(relative as Person, true, token));

  private static RelativeInfo[] ToTypedArray(
    IEnumerable<RelativeInfo> relatives,
    RelationshipType type,
    Generation generation,
    Consanguinity consanguinity) =>
      [.. relatives.Select(s => s with { Type = type, Generation = generation, Consanguinity = consanguinity })];

  private async Task<RelativeInfo[]> GetRelativeInfosAsync(Relative[] relatives, bool selectMainPhoto, CancellationToken token)
  {
    var personInfos = await Document.PersonManager.GetPersonInfosAsync(
      persons: relatives,
      selectMainPhoto: selectMainPhoto,
      token: token);

    var relativeInfos = new RelativeInfo[personInfos.Length];
    for (var i = 0; i < personInfos.Length; i++)
    {
      var relative = relatives[i];
      var personInfo = personInfos[i];
      var generation = new Generation(relative.Type);
      relativeInfos[i] = new RelativeInfo(
        relative: relative,
        names: personInfo.Names,
        mainPhoto: personInfo.MainPhoto,
        generation: generation,
        consanguinity: Consanguinity.Zero);
    }

    return relativeInfos;
  }


  internal RelativesProvider(IProjectDocument document)
    : base(document)
  {

  }

  internal override Task CreateAsync(CancellationToken token)
  {
    throw new NotSupportedException();
  }

  private static bool IsRelationshipSupported(RelationshipType? personType, RelationshipType relativeType)
  {
    var ret = personType switch
    {
      RelationshipType.Parent => relativeType switch
      {
        RelationshipType.Parent => true,
        RelationshipType.AdoptiveParent => true,
        RelationshipType.Sibling => true,
        RelationshipType.SiblingByMother => true,
        RelationshipType.SiblingByFather => true,
        _ => false
      },
      RelationshipType.Child => relativeType switch
      {
        RelationshipType.Child => true,
        RelationshipType.Spouse => true,
        _ => false
      },
      RelationshipType.Sibling or
      RelationshipType.SiblingByMother or
      RelationshipType.SiblingByFather => relativeType switch
      {
        RelationshipType.Child => true,
        RelationshipType.Spouse => true,
        _ => false
      },
      null => true,
      _ => false
    };

    return ret;
  }

  private static Generation GetNextGeneration(RelationshipType? personType, RelationshipType relativeType, Generation? generation)
  {
    var UnsupportedRelationshipException = () =>
       new ApplicationException($"Unsupported relationship {personType}->{relativeType}");

    if (personType is null)
    {
      return new Generation(relativeType);
    }

    var startGeneration = generation ?? Generation.Zero;
    var generationChanged = relativeType switch
    {
      RelationshipType.Sibling or
      RelationshipType.SiblingByFather or
      RelationshipType.SiblingByMother or
      RelationshipType.Spouse => false,
      _ => true
    };

    if (!generationChanged)
    {
      return startGeneration;
    }

    var ret = personType switch
    {
      RelationshipType.Parent => relativeType switch
      {
        RelationshipType.Child => --startGeneration,
        RelationshipType.Parent or
        RelationshipType.AdoptiveParent => ++startGeneration,
        _ => throw UnsupportedRelationshipException()
      },
      RelationshipType.Child => relativeType switch
      {
        RelationshipType.Parent => ++startGeneration,
        RelationshipType.Child => --startGeneration,
        _ => throw UnsupportedRelationshipException()
      },
      RelationshipType.Sibling or
      RelationshipType.SiblingByFather or
      RelationshipType.SiblingByMother => relativeType switch
      {
        RelationshipType.Parent => ++startGeneration,
        RelationshipType.Child => --startGeneration,
        _ => throw UnsupportedRelationshipException()
      },
      _ => throw UnsupportedRelationshipException()
    };

    return ret;
  }

  private static Consanguinity GetNextConsanguinity(RelationshipType relativeType, Generation? generation, Consanguinity? consanguinity)
  {
    var UnsupportedRelationshipException = () =>
       new ApplicationException($"Unsupported relationship type {relativeType}");

    if (generation is null || consanguinity is null)
    {
      return Consanguinity.Zero;
    }

    var startConsanguinity = consanguinity.Value;

    var consanguinityChanged = relativeType switch
    {
      RelationshipType.Sibling or
      RelationshipType.SiblingByMother or
      RelationshipType.SiblingByFather => true,
      _ => false
    };

    if (!consanguinityChanged)
    {
      return startConsanguinity;
    }

    var ret = Consanguinity.Sibling + new Consanguinity(generation.Value.Value);

    return ret;
  }

  public async Task<RelativeInfo[]> GetRelativeInfosAsync(
    RelativeInfo relativeInfo,
    bool selectMainPhoto,
    CancellationToken token)
  {
    var relatives = await Document.Relatives.GetRelativesAsync(relativeInfo, token);
    var parentTasks = relatives
      .Where(r => r.Type == RelationshipType.Parent || r.Type == RelationshipType.AdoptiveParent)
      .Select(r => Document.PersonManager.GetPersonFullInfoAsync(r, token));
    relatives = relatives
      .Where(r => IsRelationshipSupported(relativeInfo.Type, r.Type))
      .ToArray();
    var relativeInfosTask = GetRelativeInfosAsync(relatives, selectMainPhoto, token);
    await Task.WhenAll([relativeInfosTask, .. parentTasks]);
    var siblings = parentTasks
      .Select(t => t.Result)
      .SelectMany(p => p.RelativeInfos)
      .Where(r => r.Id != relativeInfo.Id)
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Where(r => IsRelationshipSupported(relativeInfo.Type, RelationshipType.Sibling))
      .Distinct(_RelativeInfoComparer);
    var siblingRelativeTasks = siblings
      .Select(s => Document.Relatives.GetRelativesAsync(s, token));
    await Task.WhenAll(siblingRelativeTasks);
    var siblingSpouses = await GetRelativeInfosAsync(
      [..siblingRelativeTasks
        .SelectMany(r => r.Result)
        .Where(r => r.Type == RelationshipType.Spouse)],
      selectMainPhoto,
      token);

    var siblingGeneration = relativeInfo.Generation;
    var siblingConsanguinity = GetNextConsanguinity(RelationshipType.Sibling, relativeInfo.Generation, relativeInfo.Consanguinity);
    var siblingSpouseGeneration = siblingGeneration;
    var siblingSpouseConsanguinity = siblingConsanguinity;

    RelativeInfo[] relativeInfos =
    [
      ..relativeInfosTask
        .Result
        .Select(r =>
        {
          var nextGeneration = GetNextGeneration(relativeInfo.Type, r.Type, relativeInfo.Generation);
          var nextConsanguinity = GetNextConsanguinity(r.Type, r.Generation, relativeInfo.Consanguinity);
          return r with { Consanguinity = nextConsanguinity, Generation = nextGeneration };
        }),
      ..siblings
        .Select(r => r with
        {
          Consanguinity = siblingConsanguinity,
          Generation = siblingGeneration,
          Type = RelationshipType.Sibling
        }),
      ..siblingSpouses
        .Select(r => r with
        {
          Consanguinity = siblingSpouseConsanguinity,
          Generation = siblingSpouseGeneration
        })
    ];

    return relativeInfos;
  }

  public async Task<RelativeInfo[]> GetRelativeInfosAsync(Person person, bool selectMainPhoto, CancellationToken token)
  {
    var relatives = await Document.Relatives.GetRelativesAsync(person, token);
    var relativeInfos = await GetRelativeInfosAsync(relatives, true, token);

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
      .Where(r => r.Type == RelationshipType.Spouse)
      .Where(r => !allParentIds.Contains(r.Id))
      .Select(r => r with
      {
        Type = RelationshipType.StepParent,
        Generation = new Generation(RelationshipType.Parent)
      })
      .Select(r => GetRelativeFullInfoAsync(r, token));
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
    var spouseTasks = relativeInfos
      .Where(r => r.Type == RelationshipType.Spouse)
      .Select(r => GetRelativeFullInfoAsync(r, token));
    var spouses = await Task.WhenAll(spouseTasks);
    var ret = spouses
      .SelectMany(s => s.RelativeInfos.Select(r => r with { Date = s.Date }))
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Where(r => !ownChildrenIds.Contains(r.Id));

    return ToTypedArray(ret, RelationshipType.StepChild, Generation.Child, Consanguinity.Zero);
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
        .Distinct(_RelativeInfoComparer)
        .Except(fatherChildren, _RelativeInfoComparer)
        .Except(motherChildren, _RelativeInfoComparer);
    var stepParentChildren = parents.Step
        .SelectMany(p => p.RelativeInfos.Select(r => r with { Date = p.Date }))
        .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
        .Distinct(_RelativeInfoComparer)
        .Except(fatherChildren, _RelativeInfoComparer)
        .Except(motherChildren, _RelativeInfoComparer);

    return new Siblings(
      Native: ToTypedArray(commonChildren, RelationshipType.Sibling, Generation.Zero, Consanguinity.Sibling),
      ByMother: ToTypedArray(motherChildren, RelationshipType.SiblingByMother, Generation.Zero, Consanguinity.Sibling),
      ByFather: ToTypedArray(fatherChildren, RelationshipType.SiblingByFather, Generation.Zero, Consanguinity.Sibling),
      Adoptive: ToTypedArray(adoptiveChildren, RelationshipType.AdoptiveSibling, Generation.Zero, Consanguinity.Sibling),
      Step: ToTypedArray(stepParentChildren, RelationshipType.StepSibling, Generation.Zero, Consanguinity.Sibling));
  }

  public RelativeInfo[] GetChildren(RelativeInfo[] relativeInfos) =>
    relativeInfos
    .Where(r => r.Type == RelationshipType.Child)
    .ToArray();

  public RelativeInfo[] GetAdoptiveChildren(RelativeInfo[] relativeInfos) =>
    relativeInfos
    .Where(r => r.Type == RelationshipType.AdoptiveChild)
    .ToArray();
}
