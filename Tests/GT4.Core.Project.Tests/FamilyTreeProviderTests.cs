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

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 2, descendantGenerations: 2, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

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

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 2, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

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

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 1, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

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

    var tree = await Provider.BuildAsync(parent, ancestorGenerations: 0, descendantGenerations: 2, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

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

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 5, descendantGenerations: 5, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, parent.Id, grandParent.Id]);
    tree.Nodes.Id().Should().NotContain(aunt.Id);
  }

  [Fact]
  public async Task Build_AttachesSpouseOfTheCenterOnTheSameGeneration()
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

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

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 1, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

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

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

    tree.Edges.Count(edge => edge.Relation == FamilyTreeRelation.Spouse).Should().Be(1);
  }

  [Fact]
  public async Task Build_LinksADescendantToBothParentsInTheTree()
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(person, child, RelationshipType.Child);
    _documentMock.AddRelationship(spouse, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 1, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(person.Id, child.Id));
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(spouse.Id, child.Id));
  }

  [Fact]
  public async Task Build_LeavesOutACoParentWhoIsNotOtherwiseInTheTree()
  {
    var person = _documentMock.CreatePerson();
    var coParent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, child, RelationshipType.Child);
    _documentMock.AddRelationship(coParent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 1, includeCollaterals: false, hiddenIds: [], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([person.Id, child.Id]);
    tree.Edges.Should().NotContain(edge => edge.FromId == coParent.Id);
  }

  [Fact]
  public async Task Build_LeavesOutTheParentsOfAMarriedInSpouse()
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();
    var spouseParent = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(spouseParent, spouse, RelationshipType.Child);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 2, descendantGenerations: 2, includeCollaterals: true, hiddenIds: [], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([person.Id, spouse.Id]);
  }

  [Fact]
  public async Task Build_WithCollaterals_LinksACousinToTheAuntsHusband()
  {
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var aunt = _documentMock.CreatePerson();
    var auntsHusband = _documentMock.CreatePerson();
    var cousin = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, aunt, RelationshipType.Child);
    _documentMock.AddRelationship(aunt, auntsHusband, RelationshipType.Spouse);
    _documentMock.AddRelationship(aunt, cousin, RelationshipType.Child);
    _documentMock.AddRelationship(auntsHusband, cousin, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 2, descendantGenerations: 2, includeCollaterals: true, hiddenIds: [], CancellationToken.None);

    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(aunt.Id, cousin.Id));
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(auntsHusband.Id, cousin.Id));
  }

  [Fact]
  public async Task Build_WithCollaterals_IncludesSiblings()
  {
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var sibling = _documentMock.CreatePerson();

    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(parent, sibling, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 1, descendantGenerations: 1, includeCollaterals: true, hiddenIds: [], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, parent.Id, sibling.Id]);
    GenerationOf(tree, sibling).Should().Be(0);
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(parent.Id, sibling.Id));
  }

  [Fact]
  public async Task Build_WithCollaterals_IncludesAuntsAndCousins()
  {
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var aunt = _documentMock.CreatePerson();
    var cousin = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, aunt, RelationshipType.Child);
    _documentMock.AddRelationship(aunt, cousin, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 2, descendantGenerations: 2, includeCollaterals: true, hiddenIds: [], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, parent.Id, grandParent.Id, aunt.Id, cousin.Id]);
    GenerationOf(tree, aunt).Should().Be(1);
    GenerationOf(tree, cousin).Should().Be(0);
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(grandParent.Id, aunt.Id));
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(aunt.Id, cousin.Id));
  }

  [Fact]
  public async Task Build_WithCollaterals_BoundsDescentByDescendantGenerations()
  {
    // descendantGenerations bounds the number of descent levels taken from the seed frontier (all
    // seeds advance together). With a single level, the grandparent reaches the aunt but not her
    // child (the cousin), which would need a second level.
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var aunt = _documentMock.CreatePerson();
    var cousin = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, aunt, RelationshipType.Child);
    _documentMock.AddRelationship(aunt, cousin, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 2, descendantGenerations: 1, includeCollaterals: true, hiddenIds: [], CancellationToken.None);

    tree.Nodes.Id().Should().Contain(aunt.Id);
    tree.Nodes.Id().Should().NotContain(cousin.Id);
  }

  [Fact]
  public async Task Build_HidingAParent_DropsTheAncestorsReachedOnlyThroughThem()
  {
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 2, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [parent.Id], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id]);
    tree.Edges.Should().BeEmpty();
  }

  [Fact]
  public async Task Build_HidingAChild_DropsTheirDescendants()
  {
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var grandChild = _documentMock.CreatePerson();

    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(child, grandChild, RelationshipType.Child);

    var tree = await Provider.BuildAsync(parent, ancestorGenerations: 0, descendantGenerations: 2, includeCollaterals: false, hiddenIds: [child.Id], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([parent.Id]);
    tree.Edges.Should().BeEmpty();
  }

  [Fact]
  public async Task Build_DoesNotAttachAHiddenSpouse()
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [spouse.Id], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([person.Id]);
    tree.Edges.Should().BeEmpty();
  }

  [Fact]
  public async Task Build_LinksAChildOnlyToTheParentWhoIsNotHidden()
  {
    var person = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(person, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(person, child, RelationshipType.Child);
    _documentMock.AddRelationship(spouse, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(person, ancestorGenerations: 0, descendantGenerations: 1, includeCollaterals: false, hiddenIds: [spouse.Id], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([person.Id, child.Id]);
    tree.Edges.Should().BeEquivalentTo([FamilyTreeEdge.ParentChild(person.Id, child.Id)]);
  }

  [Fact]
  public async Task Build_KeepsAnAncestorStillReachedByAnotherLine()
  {
    // Pedigree collapse: the common ancestor heads both the father's and the mother's line.
    var commonAncestor = _documentMock.CreatePerson();
    var paternalGrandParent = _documentMock.CreatePerson();
    var maternalGrandParent = _documentMock.CreatePerson();
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(commonAncestor, paternalGrandParent, RelationshipType.Child);
    _documentMock.AddRelationship(commonAncestor, maternalGrandParent, RelationshipType.Child);
    _documentMock.AddRelationship(paternalGrandParent, father, RelationshipType.Child);
    _documentMock.AddRelationship(maternalGrandParent, mother, RelationshipType.Child);
    _documentMock.AddRelationship(father, child, RelationshipType.Child);
    _documentMock.AddRelationship(mother, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 3, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [father.Id], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, mother.Id, maternalGrandParent.Id, commonAncestor.Id]);
    tree.Edges.Should().NotContain(edge => edge.FromId == paternalGrandParent.Id || edge.ToId == paternalGrandParent.Id);
  }

  [Fact]
  public async Task Build_WithCollaterals_KeepsASiblingStillReachedThroughTheOtherParent()
  {
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var child = _documentMock.CreatePerson();
    var sibling = _documentMock.CreatePerson();

    _documentMock.AddRelationship(father, child, RelationshipType.Child);
    _documentMock.AddRelationship(mother, child, RelationshipType.Child);
    _documentMock.AddRelationship(father, sibling, RelationshipType.Child);
    _documentMock.AddRelationship(mother, sibling, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 1, descendantGenerations: 1, includeCollaterals: true, hiddenIds: [father.Id], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, mother.Id, sibling.Id]);
    tree.Edges.Should().Contain(FamilyTreeEdge.ParentChild(mother.Id, sibling.Id));
    tree.Edges.Should().NotContain(edge => edge.FromId == father.Id || edge.ToId == father.Id);
  }

  [Fact]
  public async Task Build_ShowsAHiddenCenter()
  {
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();

    _documentMock.AddRelationship(parent, child, RelationshipType.Child);

    var tree = await Provider.BuildAsync(child, ancestorGenerations: 1, descendantGenerations: 0, includeCollaterals: false, hiddenIds: [child.Id], CancellationToken.None);

    tree.Nodes.Id().Should().BeEquivalentTo([child.Id, parent.Id]);
  }
}
