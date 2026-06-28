using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal class RelativesProvider : ProjectComponentBase, IRelativesProvider
{
  private static readonly ElementIdComparer<RelativeInfo> _RelativeInfoComparer = new();

  private async Task<RelativeFullInfo[]> GetRelativeFullInfosAsync(RelativeInfo[] relatives, CancellationToken token)
  {
    if (relatives.Length == 0)
      return [];

    var allRelativesDict = await Document.Relatives.GetRelativesForPersonsAsync(relatives, token);

    var uniqueRelativesById = allRelativesDict.Values
      .SelectMany(r => r)
      .GroupBy(r => r.Id)
      .ToDictionary(g => g.Key, g => (Person)g.First());

    PersonInfo[] personInfos = uniqueRelativesById.Count > 0
      ? await Document.PersonManager.GetPersonInfosAsync([.. uniqueRelativesById.Values], MainPhoto.Reference, token)
      : [];

    var personInfoById = personInfos.ToDictionary(p => p.Id);

    return relatives.Select(relative =>
    {
      var directRelatives = allRelativesDict.GetValueOrDefault(relative.Id, []);
      var relativeInfos = directRelatives.Select(r =>
      {
        personInfoById.TryGetValue(r.Id, out var info);
        return new RelativeInfo(
          relative: r,
          names: info?.Names ?? [],
          mainPhoto: info?.MainPhoto,
          generation: new Generation(r.Type),
          consanguinity: Consanguinity.Zero);
      }).ToArray();
      return new RelativeFullInfo(relative, relativeInfos);
    }).ToArray();
  }

  private static RelativeInfo[] ToTypedArray(
    IEnumerable<RelativeInfo> relatives,
    RelationshipType type,
    Generation generation,
    Consanguinity consanguinity) =>
      [.. relatives.Select(s => s with { Type = type, Generation = generation, Consanguinity = consanguinity })];

  private async Task<RelativeInfo[]> GetRelativeInfosAsync(Relative[] relatives, MainPhoto mainPhoto, CancellationToken token)
  {
    var personInfos = await Document.PersonManager.GetPersonInfosAsync(
      persons: relatives,
      mainPhoto: mainPhoto,
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

  private static bool IsRelationshipSupported(RelativeInfo relativeInfo, RelationshipType relativeType)
  {
    var personType = relativeInfo.Type;
    var generation = relativeInfo.Generation;
    var consanguinity = relativeInfo.Consanguinity;
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
        RelationshipType.Spouse => consanguinity == Consanguinity.Zero && generation == Generation.Child,
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
      RelationshipType.Spouse => relativeType switch
      {
        RelationshipType.Parent or
        RelationshipType.AdoptiveParent => generation == Generation.Zero && consanguinity == Consanguinity.Zero,
        _ => false
      },
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
      RelationshipType.Spouse when generation == Generation.Zero => relativeType switch
      {
        RelationshipType.Parent => ++startGeneration,
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
    MainPhoto mainPhoto,
    CancellationToken token)
  {
    var relatives = await Document.Relatives.GetRelativesAsync(relativeInfo, token);
    // Fetch only the relatives list of each parent — not their full PersonFullInfo —
    // since we only need their children to compute siblings. This avoids loading names,
    // bio, photos, and each parent's relatives' photos (many cascaded queries per parent).
    var parentRelativesTasks = relatives
      .Where(r => r.Type == RelationshipType.Parent || r.Type == RelationshipType.AdoptiveParent)
      .Select(r => Document.Relatives.GetRelativesAsync(r, token))
      .ToArray();
    relatives = relatives
      .Where(r => IsRelationshipSupported(relativeInfo, r.Type))
      .ToArray();
    var relativeInfosTask = GetRelativeInfosAsync(relatives, mainPhoto, token);
    await Task.WhenAll([relativeInfosTask, .. parentRelativesTasks]);
    var siblingRelatives = parentRelativesTasks
      .SelectMany(t => t.Result)
      .Where(r => r.Id != relativeInfo.Id)
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Where(r => IsRelationshipSupported(relativeInfo, RelationshipType.Sibling))
      .GroupBy(r => r.Id)
      .Select(g => g.First())
      .ToArray();
    var siblingInfos = siblingRelatives.Length > 0
      ? await GetRelativeInfosAsync(siblingRelatives, mainPhoto, token)
      : [];

    var siblingGeneration = relativeInfo.Generation;
    var siblingConsanguinity = GetNextConsanguinity(RelationshipType.Sibling, relativeInfo.Generation, relativeInfo.Consanguinity);

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
      ..siblingInfos
        .Select(r => r with
        {
          Consanguinity = siblingConsanguinity,
          Generation = siblingGeneration,
          Type = RelationshipType.Sibling
        })
    ];

    if (relativeInfo.Type == RelationshipType.Spouse)
    {
      RelationshipType GetInLawType(RelationshipType type) => type switch
      {
        RelationshipType.Parent => relativeInfo.BiologicalSex switch
        {
          BiologicalSex.Male => RelationshipType.HusbandParent,
          BiologicalSex.Female => RelationshipType.WifeParent,
          _ => RelationshipType.SpouseParent,
        },
        RelationshipType.Sibling or
        RelationshipType.SiblingByMother or
        RelationshipType.SiblingByFather or
        RelationshipType.AdoptiveSibling => relativeInfo.BiologicalSex switch
        {
          BiologicalSex.Male => RelationshipType.HusbandSibling,
          BiologicalSex.Female => RelationshipType.WifeSibling,
          _ => RelationshipType.SpouseSibling,
        },
        _ => type
      };
      relativeInfos = [.. relativeInfos.Select(r => r with { Type = GetInLawType(r.Type) })];
    }
    return relativeInfos;
  }

  public async Task<RelativeInfo[]> GetRelativeInfosAsync(Person person, MainPhoto mainPhoto, CancellationToken token)
  {
    var relatives = await Document.Relatives.GetRelativesAsync(person, token);
    var relativeInfos = await GetRelativeInfosAsync(relatives, mainPhoto, token);

    return relativeInfos;
  }

  public async Task<Parents> GetParentsAsync(RelativeInfo[] relativeInfos, CancellationToken token)
  {
    var parents = relativeInfos.Where(r => r.Type == RelationshipType.Parent).ToArray();
    var adoptiveParents = relativeInfos.Where(r => r.Type == RelationshipType.AdoptiveParent).ToArray();
    var combined = await GetRelativeFullInfosAsync([.. parents, .. adoptiveParents], token);
    var parentFullInfos = combined[..parents.Length];
    var adoptiveParentFullInfos = combined[parents.Length..];

    var allParentIds = new HashSet<int>([.. parentFullInfos.Select(p => p.Id), .. adoptiveParentFullInfos.Select(p => p.Id)]);
    var stepParentRelativeInfos = parentFullInfos
      .SelectMany(p => p.RelativeInfos)
      .Where(r => r.Type == RelationshipType.Spouse)
      .Where(r => !allParentIds.Contains(r.Id))
      .Select(r => r with { Type = RelationshipType.StepParent, Generation = new Generation(RelationshipType.Parent) })
      .ToArray();
    var stepParents = await GetRelativeFullInfosAsync(stepParentRelativeInfos, token);

    return new Parents(
      Native: parentFullInfos,
      Adoptive: adoptiveParentFullInfos,
      Step: stepParents);
  }

  public async Task<RelativeInfo[]> GetStepChildrenAsync(RelativeInfo[] relativeInfos, CancellationToken token)
  {
    var ownChildrenIds = relativeInfos
      .Where(r => r.Type == RelationshipType.Child || r.Type == RelationshipType.AdoptiveChild)
      .Select(r => r.Id)
      .ToHashSet();
    var spouses = relativeInfos.Where(r => r.Type == RelationshipType.Spouse).ToArray();
    var spouseFullInfos = await GetRelativeFullInfosAsync(spouses, token);
    var ret = spouseFullInfos
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
