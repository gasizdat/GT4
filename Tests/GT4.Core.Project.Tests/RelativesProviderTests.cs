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
  public async Task GetRelativeInfosAsync_Childs_Parent()
  {
    var greatGreatGrandParent = _documentMock.CreatePerson();
    var greatGrandParent = _documentMock.CreatePerson();
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(greatGreatGrandParent, greatGrandParent, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParent, grandParent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);
    var relatives = await relativesProvider.GetRelativeInfosAsync(child, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([parent.Id]);

    var rParent = relatives.SingleId(parent);
    Assert.Equal(RelationshipType.Parent, rParent.Type);
    Assert.Equal(Generation.Parent, rParent.Generation);
    Assert.Equal(Consanguinity.Zero, rParent.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParent.Id]);

    var rGrandParent = relatives.SingleId(grandParent);
    Assert.Equal(RelationshipType.Parent, rGrandParent.Type);
    Assert.Equal(new Generation(2), rGrandParent.Generation);
    Assert.Equal(Consanguinity.Zero, rGrandParent.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParent.Id]);

    var rGreatGrandParent = relatives.SingleId(greatGrandParent);
    Assert.Equal(RelationshipType.Parent, rGreatGrandParent.Type);
    Assert.Equal(new Generation(3), rGreatGrandParent.Generation);
    Assert.Equal(Consanguinity.Zero, rGreatGrandParent.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGreatGrandParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGreatGrandParent.Id]);

    var rGreatGreatGrandParent = relatives.SingleId(greatGreatGrandParent);
    Assert.Equal(RelationshipType.Parent, rGreatGreatGrandParent.Type);
    Assert.Equal(new Generation(4), rGreatGreatGrandParent.Generation);
    Assert.Equal(Consanguinity.Zero, rGreatGreatGrandParent.Consanguinity);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_Parent_Childs()
  {
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var grandChild = _documentMock.CreatePerson();
    var greatGrandChild = _documentMock.CreatePerson();
    var greatGreatGrandChild = _documentMock.CreatePerson();

    _documentMock.AddRelationship(greatGreatGrandChild, greatGrandChild, RelationshipType.Parent);
    _documentMock.AddRelationship(greatGrandChild, grandChild, RelationshipType.Parent);
    _documentMock.AddRelationship(grandChild, child, RelationshipType.Parent);
    _documentMock.AddRelationship(child, parent, RelationshipType.Parent);

    var relativesProvider = new RelativesProvider(_documentMock);

    var relatives = await relativesProvider.GetRelativeInfosAsync(parent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([child.Id]);

    var rChild = relatives.SingleId(child);
    Assert.Equal(RelationshipType.Child, rChild.Type);
    Assert.Equal(Generation.Child, rChild.Generation);
    Assert.Equal(Consanguinity.Zero, rChild.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandChild.Id]);

    var rGrandChild = relatives.SingleId(grandChild);
    Assert.Equal(RelationshipType.Child, rGrandChild.Type);
    Assert.Equal(new Generation(-2), rGrandChild.Generation);
    Assert.Equal(Consanguinity.Zero, rGrandChild.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandChild.Id]);

    var rGreatGrandChild = relatives.SingleId(greatGrandChild);
    Assert.Equal(RelationshipType.Child, rGreatGrandChild.Type);
    Assert.Equal(new Generation(-3), rGreatGrandChild.Generation);
    Assert.Equal(Consanguinity.Zero, rGreatGrandChild.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGreatGrandChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGreatGrandChild.Id]);


    var rGreatGreatGrandChild = relatives.SingleId(greatGreatGrandChild);
    Assert.Equal(RelationshipType.Child, rGreatGreatGrandChild.Type);
    Assert.Equal(new Generation(-4), rGreatGreatGrandChild.Generation);
    Assert.Equal(Consanguinity.Zero, rGreatGreatGrandChild.Consanguinity);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_Parent_Childs_Spouse()
  {
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var childSpouse = _documentMock.CreatePerson();
    var grandChild = _documentMock.CreatePerson();
    var grandChildSpouse = _documentMock.CreatePerson();

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
    Assert.Equal(RelationshipType.Child, rChild.Type);
    Assert.Equal(Generation.Child, rChild.Generation);
    Assert.Equal(Consanguinity.Zero, rChild.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandChild.Id, childSpouse.Id]);

    var rChildSpouse = relatives.SingleId(childSpouse);
    Assert.Equal(RelationshipType.Spouse, rChildSpouse.Type);
    Assert.Equal(Generation.Child, rChildSpouse.Generation);
    Assert.Equal(Consanguinity.Zero, rChildSpouse.Consanguinity);

    var rGrandChild = relatives.SingleId(grandChild);
    Assert.Equal(RelationshipType.Child, rGrandChild.Type);
    Assert.Equal(new Generation(-2), rGrandChild.Generation);
    Assert.Equal(Consanguinity.Zero, rGrandChild.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rChildSpouse, true, CancellationToken.None);
    relatives
      .Should()
      .BeEmpty();

    // Only a direct child's spouse is surfaced. A grandchild's spouse
    // (generation below Child) is intentionally not shown.
    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandChild, true, CancellationToken.None);
    relatives
      .Should()
      .BeEmpty();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task GetRelativeInfosAsync_Childs_Parent_Spouse(bool addSpouse)
  {
    var grandParent = _documentMock.CreatePerson();
    var grandParentSpouse = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var childSpouse = _documentMock.CreatePerson();

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
    Assert.Equal(RelationshipType.Spouse, rChildSpouse.Type);
    Assert.Equal(Generation.Zero, rChildSpouse.Generation);
    Assert.Equal(Consanguinity.Zero, rChildSpouse.Consanguinity);

    var rParent = relatives.SingleId(parent);
    Assert.Equal(RelationshipType.Parent, rParent.Type);
    Assert.Equal(Generation.Parent, rParent.Generation);
    Assert.Equal(Consanguinity.Zero, rParent.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParent.Id, grandParentSpouse.Id]);

    var rGrandParent = relatives.SingleId(grandParent);
    Assert.Equal(RelationshipType.Parent, rGrandParent.Type);
    Assert.Equal(new Generation(2), rGrandParent.Generation);
    Assert.Equal(Consanguinity.Zero, rGrandParent.Consanguinity);

    var rGrandParentSpouse = relatives.SingleId(grandParentSpouse);
    Assert.Equal(RelationshipType.Parent, rGrandParentSpouse.Type);
    Assert.Equal(new Generation(2), rGrandParentSpouse.Generation);
    Assert.Equal(Consanguinity.Zero, rGrandParentSpouse.Consanguinity);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task GetRelativeInfosAsync_Parent_Siblings(bool addSpouse)
  {
    var fathersFathersParent = _documentMock.CreatePerson();
    var fathersMothersParent = _documentMock.CreatePerson();
    var fathersFather = _documentMock.CreatePerson(BiologicalSex.Male);
    var fathersFathersSibling = _documentMock.CreatePerson();
    var fathersMother = _documentMock.CreatePerson(BiologicalSex.Female);
    var fathersMothersSibling = _documentMock.CreatePerson();
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var fathersSibling = _documentMock.CreatePerson();
    var mothersParent = _documentMock.CreatePerson();
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var mothersSibling = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

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
    Assert.Equal(RelationshipType.Parent, rFather.Type);
    Assert.Equal(Generation.Parent, rFather.Generation);
    Assert.Equal(Consanguinity.Zero, rFather.Consanguinity);

    var rMother = relatives.SingleId(mother);
    Assert.Equal(RelationshipType.Parent, rMother.Type);
    Assert.Equal(Generation.Parent, rMother.Generation);
    Assert.Equal(Consanguinity.Zero, rMother.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rFather, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([fathersFather.Id, fathersMother.Id, fathersSibling.Id]);

    var rFathersFather = relatives.SingleId(fathersFather);
    Assert.Equal(RelationshipType.Parent, rFathersFather.Type);
    Assert.Equal(new Generation(2), rFathersFather.Generation);
    Assert.Equal(Consanguinity.Zero, rFathersFather.Consanguinity);

    var rFathersMother = relatives.SingleId(fathersMother);
    Assert.Equal(RelationshipType.Parent, rFathersMother.Type);
    Assert.Equal(new Generation(2), rFathersMother.Generation);
    Assert.Equal(Consanguinity.Zero, rFathersMother.Consanguinity);

    var rFathersSibling = relatives.SingleId(fathersSibling);
    Assert.Equal(RelationshipType.Sibling, rFathersSibling.Type);
    Assert.Equal(Generation.Parent, rFathersSibling.Generation);
    Assert.Equal(Consanguinity.UncleAunt, rFathersSibling.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rFathersFather, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([fathersFathersParent.Id, fathersFathersSibling.Id]);

    var rFathersFathersParent = relatives.SingleId(fathersFathersParent);
    Assert.Equal(RelationshipType.Parent, rFathersFathersParent.Type);
    Assert.Equal(new Generation(3), rFathersFathersParent.Generation);
    Assert.Equal(Consanguinity.Zero, rFathersFathersParent.Consanguinity);

    var rFathersFathersSibling = relatives.SingleId(fathersFathersSibling);
    Assert.Equal(RelationshipType.Sibling, rFathersFathersSibling.Type);
    Assert.Equal(new Generation(2), rFathersFathersSibling.Generation);
    Assert.Equal(new Consanguinity(3), rFathersFathersSibling.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rMother, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([mothersParent.Id, mothersSibling.Id]);

    var rMothersParent = relatives.SingleId(mothersParent);
    Assert.Equal(RelationshipType.Parent, rMothersParent.Type);
    Assert.Equal(new Generation(2), rMothersParent.Generation);
    Assert.Equal(Consanguinity.Zero, rMothersParent.Consanguinity);

    var rMothersSibling = relatives.SingleId(mothersSibling);
    Assert.Equal(RelationshipType.Sibling, rMothersSibling.Type);
    Assert.Equal(Generation.Parent, rMothersSibling.Generation);
    Assert.Equal(Consanguinity.UncleAunt, rMothersSibling.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rFathersMother, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([fathersMothersParent.Id, fathersMothersSibling.Id]);

    var rFathersMothersParent = relatives.SingleId(fathersMothersParent);
    Assert.Equal(RelationshipType.Parent, rFathersMothersParent.Type);
    Assert.Equal(new Generation(3), rFathersMothersParent.Generation);
    Assert.Equal(Consanguinity.Zero, rFathersMothersParent.Consanguinity);

    var rFathersMothersSibling = relatives.SingleId(fathersMothersSibling);
    Assert.Equal(RelationshipType.Sibling, rFathersMothersSibling.Type);
    Assert.Equal(new Generation(2), rFathersMothersSibling.Generation);
    Assert.Equal(new Consanguinity(3), rFathersMothersSibling.Consanguinity);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_Parents_Cousins()
  {
    var greatGreatGrandParent = _documentMock.CreatePerson();
    var greatGrandParentSibling = _documentMock.CreatePerson();
    var greatGrandParentSiblingChild = _documentMock.CreatePerson();
    var greatGrandParentSiblingChildChild = _documentMock.CreatePerson();
    var greatGrandParentSiblingChildChildChild = _documentMock.CreatePerson();
    var greatGrandParent = _documentMock.CreatePerson();
    var grandParentSibling = _documentMock.CreatePerson();
    var grandParentSiblingChild = _documentMock.CreatePerson();
    var grandParentSiblingChildChild = _documentMock.CreatePerson();
    var grandParent = _documentMock.CreatePerson();
    var parentSibling = _documentMock.CreatePerson();
    var parentSiblingChild = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(greatGreatGrandParent, greatGrandParentSibling, RelationshipType.Child);
    _documentMock.AddRelationship(greatGreatGrandParent, greatGrandParent, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParentSibling, greatGrandParentSiblingChild, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParentSiblingChild, greatGrandParentSiblingChildChild, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParentSiblingChildChild, greatGrandParentSiblingChildChildChild, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParent, grandParentSibling, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParent, grandParent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParentSibling, grandParentSiblingChild, RelationshipType.Child);
    _documentMock.AddRelationship(grandParentSiblingChild, grandParentSiblingChildChild, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, parentSibling, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(parentSibling, parentSiblingChild, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);
    var relatives = await relativesProvider.GetRelativeInfosAsync(child, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([parent.Id]);

    var rParent = relatives.SingleId(parent);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParent.Id, parentSibling.Id]);

    var rGrandParent = relatives.SingleId(grandParent);
    var rParentSibling = relatives.SingleId(parentSibling);
    Assert.Equal(Generation.Parent, rParentSibling.Generation);
    Assert.Equal(Consanguinity.UncleAunt, rParentSibling.Consanguinity);
    Assert.Equal(RelationshipType.Sibling, rParentSibling.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParentSibling, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([parentSiblingChild.Id]);

    var rParentSiblingChild = relatives.SingleId(parentSiblingChild);
    Assert.Equal(Generation.Zero, rParentSiblingChild.Generation);
    Assert.Equal(Consanguinity.UncleAunt, rParentSiblingChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rParentSiblingChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParent.Id, grandParentSibling.Id]);

    var rGreatGrandParent = relatives.SingleId(greatGrandParent);
    var rGrandParentSibling = relatives.SingleId(grandParentSibling);
    Assert.Equal(new Generation(2), rGrandParentSibling.Generation);
    Assert.Equal(new Consanguinity(3), rGrandParentSibling.Consanguinity);
    Assert.Equal(RelationshipType.Sibling, rGrandParentSibling.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandParentSibling, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParentSiblingChild.Id]);

    var rGrandParentSiblingChild = relatives.SingleId(grandParentSiblingChild);
    Assert.Equal(Generation.Parent, rGrandParentSiblingChild.Generation);
    Assert.Equal(new Consanguinity(3), rGrandParentSiblingChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGrandParentSiblingChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandParentSiblingChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParentSiblingChildChild.Id]);

    var rGrandParentSiblingChildChild = relatives.SingleId(grandParentSiblingChildChild);
    Assert.Equal(Generation.Zero, rGrandParentSiblingChildChild.Generation);
    Assert.Equal(new Consanguinity(3), rGrandParentSiblingChildChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGrandParentSiblingChildChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGreatGrandParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGreatGrandParent.Id, greatGrandParentSibling.Id]);

    var rGreatGrandParentSibling = relatives.SingleId(greatGrandParentSibling);
    Assert.Equal(new Generation(3), rGreatGrandParentSibling.Generation);
    Assert.Equal(new Consanguinity(4), rGreatGrandParentSibling.Consanguinity);
    Assert.Equal(RelationshipType.Sibling, rGreatGrandParentSibling.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGreatGrandParentSibling, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParentSiblingChild.Id]);

    var rGreatGrandParentSiblingChild = relatives.SingleId(greatGrandParentSiblingChild);
    Assert.Equal(new Generation(2), rGreatGrandParentSiblingChild.Generation);
    Assert.Equal(new Consanguinity(4), rGreatGrandParentSiblingChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGreatGrandParentSiblingChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGreatGrandParentSiblingChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParentSiblingChildChild.Id]);

    var rGreatGrandParentSiblingChildChild = relatives.SingleId(greatGrandParentSiblingChildChild);
    Assert.Equal(Generation.Parent, rGreatGrandParentSiblingChildChild.Generation);
    Assert.Equal(new Consanguinity(4), rGreatGrandParentSiblingChildChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGreatGrandParentSiblingChildChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGreatGrandParentSiblingChildChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParentSiblingChildChildChild.Id]);

    var rGreatGrandParentSiblingChildChildChild = relatives.SingleId(greatGrandParentSiblingChildChildChild);
    Assert.Equal(Generation.Zero, rGreatGrandParentSiblingChildChildChild.Generation);
    Assert.Equal(new Consanguinity(4), rGreatGrandParentSiblingChildChildChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGreatGrandParentSiblingChildChildChild.Type);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_Parents_Cousins_2()
  {
    var greatGreatGrandParent = _documentMock.CreatePerson();
    var greatGrandParentSibling = _documentMock.CreatePerson();
    var greatGrandParentSiblingChild = _documentMock.CreatePerson();
    var greatGrandParentSiblingChildChild = _documentMock.CreatePerson();
    var greatGrandParentSiblingChildChildChild = _documentMock.CreatePerson();
    var greatGrandParent = _documentMock.CreatePerson();
    var grandParentSibling = _documentMock.CreatePerson();
    var grandParentSiblingChild = _documentMock.CreatePerson();
    var grandParentSiblingChildChild = _documentMock.CreatePerson();
    var grandParent = _documentMock.CreatePerson();
    var parentSibling = _documentMock.CreatePerson();
    var parentSiblingChild = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(greatGreatGrandParent, greatGrandParentSibling, RelationshipType.Child);
    _documentMock.AddRelationship(greatGreatGrandParent, greatGrandParent, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParentSibling, greatGrandParentSiblingChild, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParentSiblingChild, greatGrandParentSiblingChildChild, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParentSiblingChildChild, greatGrandParentSiblingChildChildChild, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParent, grandParentSibling, RelationshipType.Child);
    _documentMock.AddRelationship(greatGrandParent, grandParent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParentSibling, grandParentSiblingChild, RelationshipType.Child);
    _documentMock.AddRelationship(grandParentSiblingChild, grandParentSiblingChildChild, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, parentSibling, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(parentSibling, parentSiblingChild, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);
    var relatives = await relativesProvider.GetRelativeInfosAsync(greatGrandParentSiblingChildChildChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParentSiblingChildChild.Id]);

    relatives = await relativesProvider.GetRelativeInfosAsync(relatives.SingleId(greatGrandParentSiblingChildChild), true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParentSiblingChild.Id]);

    relatives = await relativesProvider.GetRelativeInfosAsync(relatives.SingleId(greatGrandParentSiblingChild), true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGrandParentSibling.Id]);

    relatives = await relativesProvider.GetRelativeInfosAsync(relatives.SingleId(greatGrandParentSibling), true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([greatGreatGrandParent.Id, greatGrandParent.Id]);

    var rGreatGrandParent = relatives.SingleId(greatGrandParent);
    Assert.Equal(new Generation(3), rGreatGrandParent.Generation);
    Assert.Equal(new Consanguinity(4), rGreatGrandParent.Consanguinity);
    Assert.Equal(RelationshipType.Sibling, rGreatGrandParent.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGreatGrandParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParent.Id, grandParentSibling.Id]);

    var rGrandParent = relatives.SingleId(grandParent);
    Assert.Equal(new Generation(2), rGrandParent.Generation);
    Assert.Equal(new Consanguinity(4), rGrandParent.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGrandParent.Type);

    var rGrandParentSibling = relatives.SingleId(grandParentSibling);
    Assert.Equal(new Generation(2), rGrandParentSibling.Generation);
    Assert.Equal(new Consanguinity(4), rGrandParentSibling.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGrandParentSibling.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandParentSibling, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParentSiblingChild.Id]);

    var rGrandParentSiblingChild = relatives.SingleId(grandParentSiblingChild);
    Assert.Equal(Generation.Parent, rGrandParentSiblingChild.Generation);
    Assert.Equal(new Consanguinity(4), rGrandParentSiblingChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGrandParentSiblingChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandParentSiblingChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParentSiblingChildChild.Id]);

    var rGrandParentSiblingChildChild = relatives.SingleId(grandParentSiblingChildChild);
    Assert.Equal(Generation.Zero, rGrandParentSiblingChildChild.Generation);
    Assert.Equal(new Consanguinity(4), rGrandParentSiblingChildChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rGrandParentSiblingChildChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rGrandParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([parent.Id, parentSibling.Id]);

    var rParent = relatives.SingleId(parent);
    Assert.Equal(Generation.Parent, rParent.Generation);
    Assert.Equal(new Consanguinity(4), rParent.Consanguinity);
    Assert.Equal(RelationshipType.Child, rParent.Type);

    var rParentSibling = relatives.SingleId(parentSibling);
    Assert.Equal(Generation.Parent, rParentSibling.Generation);
    Assert.Equal(new Consanguinity(4), rParentSibling.Consanguinity);
    Assert.Equal(RelationshipType.Child, rParentSibling.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([child.Id]);

    var rChild = relatives.SingleId(child);
    Assert.Equal(Generation.Zero, rChild.Generation);
    Assert.Equal(new Consanguinity(4), rChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rChild.Type);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParentSibling, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([parentSiblingChild.Id]);

    var rParentSiblingChild = relatives.SingleId(parentSiblingChild);
    Assert.Equal(Generation.Zero, rParentSiblingChild.Generation);
    Assert.Equal(new Consanguinity(4), rParentSiblingChild.Consanguinity);
    Assert.Equal(RelationshipType.Child, rParentSiblingChild.Type);
  }

  [Fact]
  public void GetSiblings_StepParent_ChildTypeBecomesStepSibling()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var stepChild = CreateRelative(RelationshipType.Child);
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepParentFull = new RelativeFullInfo(stepParent, [stepChild]);
    var parents = new Parents(Native: [], Adoptive: [], Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.Step
      .Id()
      .Should()
      .BeEquivalentTo([stepChild.Id]);
    siblings.Step
      .Single()
      .Type
      .Should()
      .Be(RelationshipType.StepSibling);
  }

  [Fact]
  public void GetSiblings_StepParent_AdoptiveChildTypeBecomesStepSibling()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var stepAdoptiveChild = CreateRelative(RelationshipType.AdoptiveChild);
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepParentFull = new RelativeFullInfo(stepParent, [stepAdoptiveChild]);
    var parents = new Parents(Native: [], Adoptive: [], Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.Step
      .Id()
      .Should()
      .BeEquivalentTo([stepAdoptiveChild.Id]);
  }

  [Fact]
  public void GetSiblings_StepParent_NonChildRelativesExcluded()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var stepParentSpouse = CreateRelative(RelationshipType.Spouse);
    var stepParentParent = CreateRelative(RelationshipType.Parent);
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepParentFull = new RelativeFullInfo(stepParent, [stepParentSpouse, stepParentParent]);
    var parents = new Parents(Native: [], Adoptive: [], Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.Step
      .Should()
      .BeEmpty();
  }

  [Fact]
  public void GetSiblings_StepParent_ChildSharedWithFatherOnlyExcluded()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var fatherOnlyChild = CreateRelative(RelationshipType.Child);
    var father = CreateRelative(RelationshipType.Parent) with { BiologicalSex = BiologicalSex.Male };
    var fatherFull = new RelativeFullInfo(father, [fatherOnlyChild]);
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepParentFull = new RelativeFullInfo(stepParent, [fatherOnlyChild]);
    var parents = new Parents(Native: [fatherFull], Adoptive: [], Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.ByFather
      .Id()
      .Should()
      .BeEquivalentTo([fatherOnlyChild.Id]);
    siblings.Step
      .Should()
      .BeEmpty();
  }

  [Fact]
  public void GetSiblings_StepParent_ChildSharedWithMotherOnlyExcluded()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var motherOnlyChild = CreateRelative(RelationshipType.Child);
    var mother = CreateRelative(RelationshipType.Parent) with { BiologicalSex = BiologicalSex.Female };
    var motherFull = new RelativeFullInfo(mother, [motherOnlyChild]);
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepParentFull = new RelativeFullInfo(stepParent, [motherOnlyChild]);
    var parents = new Parents(Native: [motherFull], Adoptive: [], Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.ByMother
      .Id()
      .Should()
      .BeEquivalentTo([motherOnlyChild.Id]);
    siblings.Step
      .Should()
      .BeEmpty();
  }

  [Fact]
  public void GetSiblings_StepParent_SharedChildAcrossMultipleStepParentsAppearsOnce()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var sharedStepChild = CreateRelative(RelationshipType.Child);
    var stepParent1 = CreateRelative(RelationshipType.Parent);
    var stepParent2 = CreateRelative(RelationshipType.Parent);
    var stepParentFull1 = new RelativeFullInfo(stepParent1, [sharedStepChild]);
    var stepParentFull2 = new RelativeFullInfo(stepParent2, [sharedStepChild]);
    var parents = new Parents(Native: [], Adoptive: [], Step: [stepParentFull1, stepParentFull2]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.Step
      .Should()
      .HaveCount(1);
    siblings.Step
      .Id()
      .Should()
      .BeEquivalentTo([sharedStepChild.Id]);
  }

  [Fact]
  public void GetSiblings_StepParent_DateInheritedFromStepParent()
  {
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var stepChild = CreateRelative(RelationshipType.Child);
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepParentFull = new RelativeFullInfo(stepParent, [stepChild]);
    var parents = new Parents(Native: [], Adoptive: [], Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.Step
      .Single()
      .Date
      .Should()
      .Be(stepParent.Date);
  }

  [Fact]
  public void GetSiblings_StepParent_ChildSharedWithNativeSiblingNotExcluded()
  {
    // stepParentChildren only excludes fatherChildren (father-only) and motherChildren (mother-only).
    // A child shared by both native parents is in commonChildren, not fatherChildren/motherChildren,
    // so it is not excluded — it appears in both Native and Step.
    var relativesProvider = new RelativesProvider(_documentMock);
    var person = _documentMock.CreatePerson();
    var commonChild = CreateRelative(RelationshipType.Child);
    var father = CreateRelative(RelationshipType.Parent) with { BiologicalSex = BiologicalSex.Male };
    var mother = CreateRelative(RelationshipType.Parent) with { BiologicalSex = BiologicalSex.Female };
    var fatherFull = new RelativeFullInfo(father, [commonChild]);
    var motherFull = new RelativeFullInfo(mother, [commonChild]);
    var stepParent = CreateRelative(RelationshipType.Parent);
    var stepParentFull = new RelativeFullInfo(stepParent, [commonChild]);
    var parents = new Parents(Native: [fatherFull, motherFull], Adoptive: [], Step: [stepParentFull]);

    var siblings = relativesProvider.GetSiblings(person, parents);

    siblings.Native
      .Id()
      .Should()
      .BeEquivalentTo([commonChild.Id]);
    siblings.Step
      .Id()
      .Should()
      .BeEquivalentTo([commonChild.Id]);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_Nepheu_Regression()
  {
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var child = _documentMock.CreatePerson();
    var siblingByFather = _documentMock.CreatePerson();
    var siblingByMother = _documentMock.CreatePerson();
    var nephew1 = _documentMock.CreatePerson();
    var nephew2 = _documentMock.CreatePerson();

    _documentMock.AddRelationship(child, father, RelationshipType.Parent);
    _documentMock.AddRelationship(child, mother, RelationshipType.Parent);
    _documentMock.AddRelationship(siblingByFather, father, RelationshipType.Parent);
    _documentMock.AddRelationship(siblingByMother, mother, RelationshipType.Parent);
    _documentMock.AddRelationship(siblingByFather, nephew1, RelationshipType.Child);
    _documentMock.AddRelationship(siblingByMother, nephew2, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);
    var relatives = await relativesProvider.GetRelativeInfosAsync(child, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([father.Id, mother.Id]);
    var parents = await relativesProvider.GetParentsAsync(relatives, CancellationToken.None);
    var siblings = relativesProvider.GetSiblings(child, parents);
    siblings.ByFather
      .Id()
      .Should()
      .BeEquivalentTo([siblingByFather.Id]);
    siblings.ByMother
      .Id()
      .Should()
      .BeEquivalentTo([siblingByMother.Id]);

    var rSiblingByFather = siblings.ByFather.SingleId(siblingByFather);
    Assert.Equal(RelationshipType.SiblingByFather, rSiblingByFather.Type);
    Assert.Equal(Generation.Zero, rSiblingByFather.Generation);
    Assert.Equal(Consanguinity.Sibling, rSiblingByFather.Consanguinity);

    var rSiblingByMother = siblings.ByMother.SingleId(siblingByMother);
    Assert.Equal(RelationshipType.SiblingByMother, rSiblingByMother.Type);
    Assert.Equal(Generation.Zero, rSiblingByMother.Generation);
    Assert.Equal(Consanguinity.Sibling, rSiblingByMother.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rSiblingByFather, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([nephew1.Id]);
    var rNephew1 = relatives.SingleId(nephew1);
    Assert.Equal(RelationshipType.Child, rNephew1.Type);
    Assert.Equal(Generation.Child, rNephew1.Generation);
    Assert.Equal(Consanguinity.Sibling, rNephew1.Consanguinity);

    relatives = await relativesProvider.GetRelativeInfosAsync(rSiblingByMother, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([nephew2.Id]);
    var rNephew2 = relatives.SingleId(nephew2);
    Assert.Equal(RelationshipType.Child, rNephew2.Type);
    Assert.Equal(Generation.Child, rNephew2.Generation);
    Assert.Equal(Consanguinity.Sibling, rNephew2.Consanguinity);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_UncleAuntSpouse_OnlyNestedUnderUncleAunt_Regression()
  {
    // The spouse of an uncle/aunt must appear only nested under the uncle/aunt
    // node, never on the same level as the native uncle/aunt itself.
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var uncleAunt = _documentMock.CreatePerson();
    var uncleAuntSpouse = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, uncleAunt, RelationshipType.Child);
    _documentMock.AddRelationship(uncleAunt, uncleAuntSpouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);

    var relatives = await relativesProvider.GetRelativeInfosAsync(child, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([parent.Id]);

    var rParent = relatives.SingleId(parent);

    // Expanding the parent must surface the grandparent and the uncle/aunt,
    // but NOT the uncle/aunt's spouse on the same (parent) level.
    relatives = await relativesProvider.GetRelativeInfosAsync(rParent, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([grandParent.Id, uncleAunt.Id]);
    relatives
      .Id()
      .Should()
      .NotContain(uncleAuntSpouse.Id);

    var rUncleAunt = relatives.SingleId(uncleAunt);
    Assert.Equal(RelationshipType.Sibling, rUncleAunt.Type);
    Assert.Equal(Generation.Parent, rUncleAunt.Generation);
    Assert.Equal(Consanguinity.UncleAunt, rUncleAunt.Consanguinity);

    // The uncle/aunt's spouse must appear nested under the uncle/aunt.
    relatives = await relativesProvider.GetRelativeInfosAsync(rUncleAunt, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([uncleAuntSpouse.Id]);

    var rUncleAuntSpouse = relatives.SingleId(uncleAuntSpouse);
    Assert.Equal(RelationshipType.Spouse, rUncleAuntSpouse.Type);
    Assert.Equal(Generation.Parent, rUncleAuntSpouse.Generation);
  }

  [Theory]
  [InlineData(BiologicalSex.Male, RelationshipType.HusbandParent)]
  [InlineData(BiologicalSex.Female, RelationshipType.WifeParent)]
  [InlineData(BiologicalSex.Unknown, RelationshipType.SpouseParent)]
  public async Task GetRelativeInfosAsync_SpouseParents_BecomeInLaws(BiologicalSex spouseSex, RelationshipType expectedType)
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson(spouseSex);
    var spouseFather = _documentMock.CreatePerson(BiologicalSex.Male);
    var spouseMother = _documentMock.CreatePerson(BiologicalSex.Female);

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(spouseFather, spouse, RelationshipType.Child);
    _documentMock.AddRelationship(spouseMother, spouse, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);

    var relatives = await relativesProvider.GetRelativeInfosAsync(person, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([spouse.Id]);

    var rSpouse = relatives.SingleId(spouse);
    Assert.Equal(RelationshipType.Spouse, rSpouse.Type);
    Assert.Equal(Generation.Zero, rSpouse.Generation);
    Assert.Equal(Consanguinity.Zero, rSpouse.Consanguinity);

    // Expanding a spouse surfaces the spouse's parents as in-laws, typed by
    // the spouse's biological sex (the parent's own sex distinguishes
    // father-in-law from mother-in-law at the formatter level).
    relatives = await relativesProvider.GetRelativeInfosAsync(rSpouse, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([spouseFather.Id, spouseMother.Id]);

    foreach (var inLaw in relatives)
    {
      Assert.Equal(expectedType, inLaw.Type);
      Assert.Equal(Generation.Parent, inLaw.Generation);
      Assert.Equal(Consanguinity.Zero, inLaw.Consanguinity);
    }
  }

  [Fact]
  public async Task GetRelativeInfosAsync_SpouseExpansion_ExcludesSelfAndSharedChildren()
  {
    // Expanding a spouse must surface only the spouse's parents — never the
    // back-reference to the person, nor children shared with the person.
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson(BiologicalSex.Female);
    var spouseParent = _documentMock.CreatePerson();
    var sharedChild = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(spouseParent, spouse, RelationshipType.Child);
    _documentMock.AddRelationship(spouse, sharedChild, RelationshipType.Child);
    _documentMock.AddRelationship(person, sharedChild, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);

    var relatives = await relativesProvider.GetRelativeInfosAsync(person, true, CancellationToken.None);
    var rSpouse = relatives.SingleId(spouse);

    relatives = await relativesProvider.GetRelativeInfosAsync(rSpouse, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([spouseParent.Id]);

    Assert.Equal(RelationshipType.WifeParent, relatives.SingleId(spouseParent).Type);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_CousinSpouse_NotSurfaced()
  {
    // The Child -> Spouse rule requires consanguinity 0, so a cousin's spouse
    // is never surfaced, while the cousin's own child still is.
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var uncleAunt = _documentMock.CreatePerson();
    var cousin = _documentMock.CreatePerson();
    var cousinSpouse = _documentMock.CreatePerson();
    var cousinChild = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, uncleAunt, RelationshipType.Child);
    _documentMock.AddRelationship(uncleAunt, cousin, RelationshipType.Child);
    _documentMock.AddRelationship(cousin, cousinSpouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(cousin, cousinChild, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var relativesProvider = new RelativesProvider(_documentMock);

    var relatives = await relativesProvider.GetRelativeInfosAsync(child, true, CancellationToken.None);
    var rParent = relatives.SingleId(parent);

    relatives = await relativesProvider.GetRelativeInfosAsync(rParent, true, CancellationToken.None);
    var rUncleAunt = relatives.SingleId(uncleAunt);

    relatives = await relativesProvider.GetRelativeInfosAsync(rUncleAunt, true, CancellationToken.None);
    var rCousin = relatives.SingleId(cousin);
    Assert.Equal(RelationshipType.Child, rCousin.Type);
    Assert.Equal(Generation.Zero, rCousin.Generation);
    Assert.Equal(Consanguinity.UncleAunt, rCousin.Consanguinity);

    // The cousin's spouse must not appear; the cousin's child must.
    relatives = await relativesProvider.GetRelativeInfosAsync(rCousin, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([cousinChild.Id]);
    relatives
      .Id()
      .Should()
      .NotContain(cousinSpouse.Id);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_DirectChildSpouse_Surfaced()
  {
    // A direct child's spouse (consanguinity 0, Child generation) is surfaced.
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var childSpouse = _documentMock.CreatePerson();

    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(child, childSpouse, RelationshipType.Spouse);

    var relativesProvider = new RelativesProvider(_documentMock);

    var relatives = await relativesProvider.GetRelativeInfosAsync(parent, true, CancellationToken.None);
    var rChild = relatives.SingleId(child);

    relatives = await relativesProvider.GetRelativeInfosAsync(rChild, true, CancellationToken.None);
    relatives
      .Id()
      .Should()
      .BeEquivalentTo([childSpouse.Id]);

    var rChildSpouse = relatives.SingleId(childSpouse);
    Assert.Equal(RelationshipType.Spouse, rChildSpouse.Type);
    Assert.Equal(Generation.Child, rChildSpouse.Generation);
    Assert.Equal(Consanguinity.Zero, rChildSpouse.Consanguinity);
  }
}
