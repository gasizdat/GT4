using AutoFixture;
using FluentAssertions;
using GT4.Core.Project.Dto;
using Xunit;

namespace GT4.Core.Project.Tests;

public class RelativesProviderTests
{
  private readonly Fixture _fixture = new();
  private readonly ProjectDocumentMock _documentMock = new();
  private RelativeInfo CreateRelative(RelationshipType type) =>
    _fixture.Create<RelativeInfo>() with
    {
      Id = _documentMock.GetNewId(),
      Type = type,
    };

  [Fact]
  public void GetChildren_FiltersOnlyChildRelationship()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var child = CreateRelative(RelationshipType.Child);
    var adoptiveChild = CreateRelative(RelationshipType.AdoptiveChild);
    var spouse = CreateRelative(RelationshipType.Spouse);
    var result = relativesProvider.GetChildren([child, adoptiveChild, spouse]);

    result
      .Should()
      .ContainSingle()
      .Which
      .Should()
      .Be(child);
  }

  [Fact]
  public void GetAdoptiveChildren_FiltersOnlyAdoptiveChildRelationship()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var child = CreateRelative(RelationshipType.Child);
    var adoptiveChild = CreateRelative(RelationshipType.AdoptiveChild);
    var spouse = CreateRelative(RelationshipType.Spouse);
    var result = relativesProvider.GetAdoptiveChildren([child, adoptiveChild, spouse]);

    result
      .Should()
      .ContainSingle()
      .Which
      .Should()
      .Be(adoptiveChild);
  }

  [Fact]
  public void GetSiblings_CategorizesNativeByParentsAndStepAndAdoptive()
  {
    var relativesProvider = new RelativesProvider(_documentMock);

    // Person under test
    var person = _documentMock.CreatePerson();
    // Child entries for parents: create RelativeInfo entries representing children.
    var commonChild = CreateRelative(RelationshipType.Child);
    var childByMother = CreateRelative(RelationshipType.Child);
    // Native parents: father and mother
    var father = CreateRelative(RelationshipType.Parent) with { BiologicalSex = BiologicalSex.Male };
    var mother = CreateRelative(RelationshipType.Parent) with { BiologicalSex = BiologicalSex.Female };
    // Parents' RelativeInfos (their children)
    var fatherFull = new RelativeFullInfo(father, [commonChild]);
    var motherFull = new RelativeFullInfo(mother, [commonChild, childByMother]);
    // Adoptive parents: include an adoptive child with different id that should be counted in Adoptive siblings
    var adoptiveParent = CreateRelative(RelationshipType.Parent);
    var adoptiveChild = CreateRelative(RelationshipType.AdoptiveChild);
    var adoptiveParentFull = new RelativeFullInfo(adoptiveParent, [adoptiveChild]);
    // Step parent: represented as RelativeFullInfo where the parent has spouse links that are not in allParentIds
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepChild = CreateRelative(RelationshipType.Child);
    var stepParentFull = new RelativeFullInfo(stepParent, [stepChild]);
    var parents = new Parents(
      Native: [fatherFull, motherFull],
      Adoptive: [adoptiveParentFull],
      Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings
      .Native
      .Id()
      .Should()
      .BeEquivalentTo([commonChild.Id]);

    siblings
      .ByMother
      .Id()
      .Should()
      .BeEquivalentTo([childByMother.Id]);

    // ByFather should be empty because both parents share commonChild 
    siblings
      .ByFather
      .Should()
      .BeEmpty();

    siblings
      .Adoptive
      .Id()
      .Should()
      .BeEquivalentTo([adoptiveChild.Id]);

    siblings
      .Step
      .Id()
      .Should()
      .BeEquivalentTo([stepChild.Id]);
  }

  [Fact]
  public async void GetRelativeInfosAsync_Parent_Childs()
  {
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var son = _documentMock.CreatePerson(BiologicalSex.Male);
    var grandDaughter = _documentMock.CreatePerson(BiologicalSex.Female);
    var grandGrandChild = _documentMock.CreatePerson();

    _documentMock.AddRelationship(father, son, RelationshipType.Child);
    _documentMock.AddRelationship(mother, son, RelationshipType.Child);
    _documentMock.AddRelationship(son, grandDaughter, RelationshipType.Child);
    _documentMock.AddRelationship(grandDaughter, grandGrandChild, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);

    foreach (var parent in new[] { father, mother })
    {
      var relatives = await relativesProvider.GetRelativeInfosAsync(parent, true, CancellationToken.None);
      relatives
        .Id()
        .Should()
        .BeEquivalentTo([son.Id]);
      Assert.Equal(relatives[0].Generation, Generation.Child);
      Assert.Equal(relatives[0].Consanguinity, Consanguinity.Zero);

      relatives = await relativesProvider.GetRelativeInfosAsync(relatives[0], true, CancellationToken.None);
      relatives
        .Id()
        .Should()
        .BeEquivalentTo([grandDaughter.Id]);
      Assert.Equal(relatives[0].Generation, new Generation(-2));
      Assert.Equal(relatives[0].Consanguinity, Consanguinity.Zero);

      relatives = await relativesProvider.GetRelativeInfosAsync(relatives[0], true, CancellationToken.None);
      relatives
        .Id()
        .Should()
        .BeEquivalentTo([grandGrandChild.Id]);
      Assert.Equal(relatives[0].Generation, new Generation(-3));
      Assert.Equal(relatives[0].Consanguinity, Consanguinity.Zero);

      relatives = await relativesProvider.GetRelativeInfosAsync(relatives[0], true, CancellationToken.None);
      relatives
        .Should()
        .BeEmpty();
    }
  }

  [Fact]
  public async void GetRelativeInfosAsync_Childs_Parent()
  {
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var son = _documentMock.CreatePerson(BiologicalSex.Male);
    var grandDaughter = _documentMock.CreatePerson(BiologicalSex.Female);
    var grandGrandChild = _documentMock.CreatePerson();

    _documentMock.AddRelationship(father, son, RelationshipType.Child);
    _documentMock.AddRelationship(mother, son, RelationshipType.Child);
    _documentMock.AddRelationship(son, grandDaughter, RelationshipType.Child);
    _documentMock.AddRelationship(grandDaughter, grandGrandChild, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);
    var relatives = await relativesProvider.GetRelativeInfosAsync(grandGrandChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandDaughter.Id]);
    Assert.Equal(relatives[0].Generation, Generation.Parent);
    Assert.Equal(relatives[0].Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(relatives[0], true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([son.Id]);
    Assert.Equal(relatives[0].Generation, new Generation(2));
    Assert.Equal(relatives[0].Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(relatives[0], true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([mother.Id, father.Id]);
    Assert.Equal(relatives[0].Generation, new Generation(3));
    Assert.Equal(relatives[0].Consanguinity, Consanguinity.Zero);
    Assert.Equal(relatives[1].Generation, new Generation(3));
    Assert.Equal(relatives[1].Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(relatives[0], true, CancellationToken.None);
    relatives
      .Should()
      .BeEmpty();
  }
}