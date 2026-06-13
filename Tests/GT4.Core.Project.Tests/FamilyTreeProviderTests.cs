using FluentAssertions;
using GT4.Core.Project.Dto;
using Xunit;

namespace GT4.Core.Project.Tests;

public class FamilyTreeProviderTests
{
  private readonly ProjectDocumentMock _documentMock = new();

  private FamilyTreeProvider Provider => new(_documentMock);

  private int GenerationOf(FamilyTree tree, Person person) =>
    tree.Nodes.Single(node => node.Id == person.Id).Generation;

  [Fact]
  public async Task Build_CentersOnTheRequestedPerson()
  {
    var person = _documentMock.CreatePerson();

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 2, descendantGenerations: 2, CancellationToken.None);

    tree.CenterId.Should().Be(person.Id);
    tree.Nodes.Id().Should().BeEquivalentTo([person.Id]);
    GenerationOf(tree, person).Should().Be(0);
    tree.Edges.Should().BeEmpty();
  }

  [Fact]
  public async Task Build_CollectsAncestorsUpToDepthOnly()
  {
    var greatGrandParent = _documentMock.CreatePerson();
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(greatGrandParent, grandParent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 2, descendantGenerations: 0, CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, parent.Id, grandParent.Id]);
    GenerationOf(tree, parent).Should().Be(1);
    GenerationOf(tree, grandParent).Should().Be(2);
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(parent.Id, child.Id));
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(grandParent.Id, parent.Id));
  }

  [Fact]
  public async Task Build_CollectsBothParentLines()
  {
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(father, child, RelationshipType.Child);
    _documentMock.AddRelationship(mother, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 1, descendantGenerations: 0, CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, father.Id, mother.Id]);
    GenerationOf(tree, father).Should().Be(1);
    GenerationOf(tree, mother).Should().Be(1);
  }

  [Fact]
  public async Task Build_CollectsDescendantsUpToDepthOnly()
  {
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var grandChild = _documentMock.CreatePerson();
    var greatGrandChild = _documentMock.CreatePerson();

    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(child, grandChild, RelationshipType.Child);
    _documentMock.AddRelationship(grandChild, greatGrandChild, RelationshipType.Child);

    var tree = await Provider.BuildAsync(parent, ancestorGenerations: 0, descendantGenerations: 2, CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([parent.Id, child.Id, grandChild.Id]);
    GenerationOf(tree, child).Should().Be(-1);
    GenerationOf(tree, grandChild).Should().Be(-2);
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(parent.Id, child.Id));
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(child.Id, grandChild.Id));
  }

  [Fact]
  public async Task Build_DoesNotPullInSiblingsOrCousinsThroughAncestors()
  {
    // Seeding both passes from the centre keeps the chart a clean ancestor/descendant bow-tie:
    // an aunt (the grandparent's other child) must not appear.
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var aunt = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, aunt, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 5, descendantGenerations: 5, CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, parent.Id, grandParent.Id]);
    tree.Nodes.Id().Should().NotContain(aunt.Id);
  }

  [Fact]
  public async Task Build_AttachesSpouseOfTheCenterOnTheSameGeneration()
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 0, CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([person.Id, spouse.Id]);
    GenerationOf(tree, spouse).Should().Be(0);
    tree.Edges.Should().ContainSingle()
      .Which.Should().Be(FamilyTreeEdge.Spouse(person.Id, spouse.Id));
  }

  [Fact]
  public async Task Build_AttachesSpouseOfAnAncestor()
  {
    var parent = _documentMock.CreatePerson();
    var parentSpouse = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(parent, parentSpouse, RelationshipType.Spouse);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 1, descendantGenerations: 0, CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, parent.Id, parentSpouse.Id]);
    GenerationOf(tree, parentSpouse).Should().Be(1);
    tree.Edges.Should().Contain(FamilyTreeEdge.Spouse(parent.Id, parentSpouse.Id));
  }

  [Fact]
  public async Task Build_DeduplicatesTheSpouseEdge()
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();

    // The mock records the reciprocal Spouse link, so both endpoints report the marriage.
    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 0, CancellationToken.None);

    tree.Edges.Count(edge => edge.Relation == FamilyTreeRelation.Spouse).Should().Be(1);
  }
}
