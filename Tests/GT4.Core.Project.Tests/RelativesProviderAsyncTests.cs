using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Covers the asynchronous resolution methods of <see cref="RelativesProvider"/>
/// (<c>GetParentsAsync</c>, <c>GetStepChildrenAsync</c>) over the in-memory
/// <see cref="ProjectDocumentMock"/> graph.
/// </summary>
public sealed class RelativesProviderAsyncTests
{
  private readonly ProjectDocumentMock _mock = new();
  private CancellationToken Token => TestContext.Current.CancellationToken;

  private static RelativeInfo AsParent(PersonInfo person) =>
    new(person, RelationshipType.Parent, null, Generation.Parent, Consanguinity.Zero);

  private static RelativeInfo AsSpouse(PersonInfo person) =>
    new(person, RelationshipType.Spouse, null, Generation.Zero, Consanguinity.Zero);

  private static RelativeInfo AsChild(PersonInfo person) =>
    new(person, RelationshipType.Child, null, Generation.Child, Consanguinity.Zero);

  [Fact]
  public async Task GetParentsAsync_ClassifiesNativeAndAdoptiveAndStep()
  {
    var person = _mock.CreatePerson();
    var father = _mock.CreatePerson(BiologicalSex.Male);
    var mother = _mock.CreatePerson(BiologicalSex.Female);
    var adoptive = _mock.CreatePerson();
    var stepMother = _mock.CreatePerson(BiologicalSex.Female);

    _mock.AddRelationship(person, father, RelationshipType.Parent);
    _mock.AddRelationship(person, mother, RelationshipType.Parent);
    _mock.AddRelationship(person, adoptive, RelationshipType.AdoptiveParent);
    // The father has a spouse who is not one of the person's parents -> step parent.
    _mock.AddRelationship(father, stepMother, RelationshipType.Spouse);

    var provider = new RelativesProvider(_mock);

    var parents = await provider.GetParentsAsync(
      [AsParent(father), AsParent(mother), new RelativeInfo(adoptive, RelationshipType.AdoptiveParent, null, Generation.Parent, Consanguinity.Zero)],
      Token);

    parents.Native.Id().Should().BeEquivalentTo([father.Id, mother.Id]);
    parents.Adoptive.Id().Should().BeEquivalentTo([adoptive.Id]);
    parents.Step.Id().Should().BeEquivalentTo([stepMother.Id]);
    parents.Step.Should().OnlyContain(p => p.Type == RelationshipType.StepParent);
  }

  [Fact]
  public async Task GetParentsAsync_NoStepParents_WhenSpouseIsAlsoAParent()
  {
    var person = _mock.CreatePerson();
    var father = _mock.CreatePerson(BiologicalSex.Male);
    var mother = _mock.CreatePerson(BiologicalSex.Female);

    _mock.AddRelationship(person, father, RelationshipType.Parent);
    _mock.AddRelationship(person, mother, RelationshipType.Parent);
    // The parents are married to each other; neither is a step parent.
    _mock.AddRelationship(father, mother, RelationshipType.Spouse);

    var provider = new RelativesProvider(_mock);

    var parents = await provider.GetParentsAsync([AsParent(father), AsParent(mother)], Token);

    parents.Native.Id().Should().BeEquivalentTo([father.Id, mother.Id]);
    parents.Step.Should().BeEmpty();
  }

  [Fact]
  public async Task GetStepChildrenAsync_ReturnsSpousesChildren_ExcludingOwn()
  {
    var person = _mock.CreatePerson();
    var spouse = _mock.CreatePerson();
    var stepChild = _mock.CreatePerson();
    var sharedChild = _mock.CreatePerson();

    // The spouse brings stepChild; sharedChild is a child of both and must NOT count as a step child.
    _mock.AddRelationship(spouse, stepChild, RelationshipType.Child);
    _mock.AddRelationship(person, sharedChild, RelationshipType.Child);
    _mock.AddRelationship(spouse, sharedChild, RelationshipType.Child);

    var provider = new RelativesProvider(_mock);

    var stepChildren = await provider.GetStepChildrenAsync(
      [AsSpouse(spouse), AsChild(sharedChild)], Token);

    stepChildren.Id().Should().BeEquivalentTo([stepChild.Id]);
    stepChildren.Should().OnlyContain(r => r.Type == RelationshipType.StepChild);
  }

  [Fact]
  public async Task GetStepChildrenAsync_NoSpouses_ReturnsEmpty()
  {
    var person = _mock.CreatePerson();
    var child = _mock.CreatePerson();
    _mock.AddRelationship(person, child, RelationshipType.Child);

    var provider = new RelativesProvider(_mock);

    var stepChildren = await provider.GetStepChildrenAsync([AsChild(child)], Token);

    stepChildren.Should().BeEmpty();
  }
}
