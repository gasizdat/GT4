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

      var rSon = relatives.SingleId(son);
      Assert.Equal(rSon.Generation, Generation.Child);
      Assert.Equal(rSon.Consanguinity, Consanguinity.Zero);

      relatives = await relativesProvider.GetRelativeInfosAsync(rSon, true, CancellationToken.None);
      relatives
        .Id()
        .Should()
        .BeEquivalentTo([grandDaughter.Id]);

      var rGrandDaughter = relatives.SingleId(grandDaughter);
      Assert.Equal(rGrandDaughter.Generation, new Generation(-2));
      Assert.Equal(rGrandDaughter.Consanguinity, Consanguinity.Zero);

      relatives = await relativesProvider.GetRelativeInfosAsync(rGrandDaughter, true, CancellationToken.None);
      relatives
        .Id()
        .Should()
        .BeEquivalentTo([grandGrandChild.Id]);

      var rGrandGrandChild = relatives.SingleId(grandGrandChild);
      Assert.Equal(rGrandGrandChild.Generation, new Generation(-3));
      Assert.Equal(rGrandGrandChild.Consanguinity, Consanguinity.Zero);

      relatives = await relativesProvider.GetRelativeInfosAsync(rGrandGrandChild, true, CancellationToken.None);
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

    var rGrandDaughter = relatives.SingleId(grandDaughter);
    Assert.Equal(rGrandDaughter.Generation, Generation.Parent);
    Assert.Equal(rGrandDaughter.Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandDaughter, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([son.Id]);

    var rSon = relatives.SingleId(son);
    Assert.Equal(rSon.Generation, new Generation(2));
    Assert.Equal(rSon.Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(rSon, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([mother.Id, father.Id]);

    var rFather = relatives.SingleId(father);
    Assert.Equal(rFather.Generation, new Generation(3));
    Assert.Equal(rFather.Consanguinity, Consanguinity.Zero);

    var rMother = relatives.SingleId(mother);
    Assert.Equal(rMother.Generation, new Generation(3));
    Assert.Equal(rMother.Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(rFather, true, CancellationToken.None);
    relatives
      .Should()
      .BeEmpty();

    relatives = await relativesProvider.GetRelativeInfosAsync(rMother, true, CancellationToken.None);
    relatives
      .Should()
      .BeEmpty();
  }

  [Fact]
  public async void GetRelativeInfosAsync_Parent_Childs_Spouse()
  {
    var parent = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var child = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var childSpouse = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var grandChild = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var grandChildSpouse = _documentMock.CreatePerson(BiologicalSex.Unknown);

    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(child, childSpouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(child, grandChild, RelationshipType.Child);
    _documentMock.AddRelationship(grandChild, grandChildSpouse, RelationshipType.Spouse);

    var relativesProvider = new RelativesProvider(_documentMock);

    var relatives = await relativesProvider.GetRelativeInfosAsync(parent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([child.Id]);

    var rChild = relatives.SingleId(child);
    Assert.Equal(rChild.Generation, Generation.Child);
    Assert.Equal(rChild.Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(rChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandChild.Id, childSpouse.Id]);

    var rChildSpouse = relatives.SingleId(childSpouse);
    Assert.Equal(rChildSpouse.Generation, Generation.Child);
    Assert.Equal(rChildSpouse.Consanguinity, Consanguinity.Zero);

    var rGrandChild = relatives.SingleId(grandChild);
    Assert.Equal(rGrandChild.Generation, new Generation(-2));
    Assert.Equal(rGrandChild.Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(rChildSpouse, true, CancellationToken.None);
    relatives
      .Should()
      .BeEmpty();

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandChildSpouse.Id]);

    var rGrandChildSpouse = relatives.SingleId(grandChildSpouse);
    Assert.Equal(rGrandChildSpouse.Generation, new Generation(-2));
    Assert.Equal(rGrandChildSpouse.Consanguinity, Consanguinity.Zero);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async void GetRelativeInfosAsync_Childs_Parent_Spouse(bool addSpouse)
  {
    var grandParent = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var grandParentSpouse = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var parent = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var child = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var childSpouse = _documentMock.CreatePerson(BiologicalSex.Unknown);

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParentSpouse, parent, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(child, childSpouse, RelationshipType.Spouse);

    if (addSpouse)
    {
      _documentMock.AddRelationship(grandParent, grandParentSpouse, RelationshipType.Spouse);
    }

    var relativesProvider = new RelativesProvider(_documentMock);
    var relatives = await relativesProvider.GetRelativeInfosAsync(child, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([parent.Id, childSpouse.Id]);

    var rChildSpouse = relatives.SingleId(childSpouse);
    Assert.Equal(rChildSpouse.Generation, Generation.Zero);
    Assert.Equal(rChildSpouse.Consanguinity, Consanguinity.Zero); 
    
    var rParent = relatives.SingleId(parent);
    Assert.Equal(rParent.Generation, Generation.Parent);
    Assert.Equal(rParent.Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParent.Id, grandParentSpouse.Id]);

    var rGrandParent = relatives.SingleId(grandParent);
    Assert.Equal(rGrandParent.Generation, new Generation(2));
    Assert.Equal(rGrandParent.Consanguinity, Consanguinity.Zero); 
    
    var rGrandParentSpouse = relatives.SingleId(grandParentSpouse);
    Assert.Equal(rGrandParentSpouse.Generation, new Generation(2));
    Assert.Equal(rGrandParentSpouse.Consanguinity, Consanguinity.Zero);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async void GetRelativeInfosAsync_Parent_Siblings(bool addSpouse)
  {
    var fathersFathersParent = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var fathersMothersParent = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var fathersFather = _documentMock.CreatePerson(BiologicalSex.Male);
    var fathersFathersSibling = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var fathersMother = _documentMock.CreatePerson(BiologicalSex.Female);
    var fathersMothersSibling = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var fathersSibling = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var mothersParent = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var mothersSibling = _documentMock.CreatePerson(BiologicalSex.Unknown);
    var child = _documentMock.CreatePerson(BiologicalSex.Unknown);

    _documentMock.AddRelationship(fathersFathersParent, fathersFather, RelationshipType.Child);
    _documentMock.AddRelationship(fathersFathersParent, fathersFathersSibling, RelationshipType.Child);
    _documentMock.AddRelationship(fathersMothersParent, fathersMother, RelationshipType.Child);
    _documentMock.AddRelationship(fathersMothersParent, fathersMothersSibling, RelationshipType.Child);
    _documentMock.AddRelationship(fathersFather, father, RelationshipType.Child);
    _documentMock.AddRelationship(fathersFather, fathersSibling, RelationshipType.Child);
    _documentMock.AddRelationship(fathersMother, father, RelationshipType.Child);
    _documentMock.AddRelationship(mothersParent, mother, RelationshipType.Child);
    _documentMock.AddRelationship(mothersParent, mothersSibling, RelationshipType.Child);
    _documentMock.AddRelationship(father, child, RelationshipType.Child);
    _documentMock.AddRelationship(mother, child, RelationshipType.Child);

    if (addSpouse)
    {
      _documentMock.AddRelationship(fathersFather, fathersMother, RelationshipType.Spouse);
      _documentMock.AddRelationship(father, mother, RelationshipType.Spouse);
    }

    var relativesProvider = new RelativesProvider(_documentMock);
    var relatives = await relativesProvider.GetRelativeInfosAsync(child, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([father.Id, mother.Id]);

    var rFather = relatives.SingleId(father);
    Assert.Equal(rFather.Generation, Generation.Parent);
    Assert.Equal(rFather.Consanguinity, Consanguinity.Zero);

    var rMother = relatives.SingleId(mother);
    Assert.Equal(rMother.Generation, Generation.Parent);
    Assert.Equal(rMother.Consanguinity, Consanguinity.Zero);

    relatives = await relativesProvider.GetRelativeInfosAsync(rFather, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([fathersFather.Id, fathersMother.Id, fathersSibling.Id]);

    var rFathersFather = relatives.SingleId(fathersFather);
    Assert.Equal(rFathersFather.Generation, new Generation(2));
    Assert.Equal(rFathersFather.Consanguinity, Consanguinity.Zero);

    var rFathersMother = relatives.SingleId(fathersMother);
    Assert.Equal(rFathersMother.Generation, new Generation(2));
    Assert.Equal(rFathersMother.Consanguinity, Consanguinity.Zero);

    var rFathersSibling = relatives.SingleId(fathersSibling);
    Assert.Equal(rFathersSibling.Generation, Generation.Parent);
    Assert.Equal(rFathersSibling.Consanguinity, Consanguinity.UncleAunt);

    relatives = await relativesProvider.GetRelativeInfosAsync(rFathersFather, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([fathersFathersParent.Id, fathersFathersSibling.Id]);

    var rFathersFathersParent = relatives.SingleId(fathersFathersParent);
    Assert.Equal(rFathersFathersParent.Generation, new Generation(3));
    Assert.Equal(rFathersFathersParent.Consanguinity, Consanguinity.Zero);

    var rFathersFathersSibling = relatives.SingleId(fathersFathersSibling);
    Assert.Equal(rFathersFathersSibling.Generation, new Generation(2));
    Assert.Equal(rFathersFathersSibling.Consanguinity, Consanguinity.UncleAunt);

    relatives = await relativesProvider.GetRelativeInfosAsync(rMother, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([mothersParent.Id, mothersSibling.Id]);

    var rMothersParent = relatives.SingleId(mothersParent);
    Assert.Equal(rMothersParent.Generation, new Generation(2));
    Assert.Equal(rMothersParent.Consanguinity, Consanguinity.Zero);

    var rMothersSibling = relatives.SingleId(mothersSibling);
    Assert.Equal(rMothersSibling.Generation, Generation.Parent);
    Assert.Equal(rMothersSibling.Consanguinity, Consanguinity.UncleAunt);
  }
}