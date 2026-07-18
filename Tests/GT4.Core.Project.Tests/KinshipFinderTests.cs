using FluentAssertions;
using GT4.Core.Project.Dto;
using Xunit;

namespace GT4.Core.Project.Tests;

public class KinshipFinderTests
{
  private readonly ProjectDocumentMock _documentMock = new();

  private KinshipFinder Finder => new(_documentMock);

  [Fact]
  public async Task FindPathAsync_DirectParent_ReturnsSingleHop()
  {
    var child = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    _documentMock.AddRelationship(child, parent, RelationshipType.Parent);

    var path = await Finder.FindPathAsync(child, parent, CancellationToken.None);

    path.Should().NotBeNull();
    path!.Should().ContainSingle();
    path[0].Id.Should().Be(parent.Id);
    path[0].Type.Should().Be(RelationshipType.Parent);
    path[0].Generation.Should().Be(Generation.Parent);
  }

  [Fact]
  public async Task FindPathAsync_Sibling_ReturnsSingleHopWithSiblingConsanguinity()
  {
    // The subject's own siblings are seeded as roots (mirroring PersonPage.AssembleRoots), not
    // discovered via RelativesProvider's generic recursive expansion. Both parents must be shared
    // (and sexed) for GetSiblings to classify the pair as full (Native), not half-, siblings.
    var father = _documentMock.CreatePerson(BiologicalSex.Male);
    var mother = _documentMock.CreatePerson(BiologicalSex.Female);
    var personA = _documentMock.CreatePerson();
    var personB = _documentMock.CreatePerson();
    _documentMock.AddRelationship(father, personA, RelationshipType.Child);
    _documentMock.AddRelationship(mother, personA, RelationshipType.Child);
    _documentMock.AddRelationship(father, personB, RelationshipType.Child);
    _documentMock.AddRelationship(mother, personB, RelationshipType.Child);

    var path = await Finder.FindPathAsync(personA, personB, CancellationToken.None);

    path.Should().NotBeNull();
    path!.Should().ContainSingle();
    path[0].Id.Should().Be(personB.Id);
    path[0].Type.Should().Be(RelationshipType.Sibling);
    path[0].Generation.Should().Be(Generation.Zero);
    path[0].Consanguinity.Should().Be(Consanguinity.Sibling);
  }

  [Fact]
  public async Task FindPathAsync_Grandparent_ReturnsTwoHopChain()
  {
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    _documentMock.AddRelationship(parent, grandParent, RelationshipType.Parent);
    _documentMock.AddRelationship(child, parent, RelationshipType.Parent);

    var path = await Finder.FindPathAsync(child, grandParent, CancellationToken.None);

    path.Should().NotBeNull();
    path!.Select(r => r.Id).Should().BeEquivalentTo([parent.Id, grandParent.Id], o => o.WithStrictOrdering());
    path[^1].Type.Should().Be(RelationshipType.Parent);
    path[^1].Generation.Should().Be(new Generation(2));
  }

  [Fact]
  public async Task FindPathAsync_FirstCousin_ReturnsFourHopChainNamedAsCousin()
  {
    var grandParent = _documentMock.CreatePerson();
    var parent = _documentMock.CreatePerson();
    var parentSibling = _documentMock.CreatePerson();
    var child = _documentMock.CreatePerson();
    var cousin = _documentMock.CreatePerson();
    _documentMock.AddRelationship(grandParent, parent, RelationshipType.Child);
    _documentMock.AddRelationship(grandParent, parentSibling, RelationshipType.Child);
    _documentMock.AddRelationship(parent, child, RelationshipType.Child);
    _documentMock.AddRelationship(parentSibling, cousin, RelationshipType.Child);

    var path = await Finder.FindPathAsync(child, cousin, CancellationToken.None);

    path.Should().NotBeNull();
    path![^1].Id.Should().Be(cousin.Id);
    path[^1].Type.Should().Be(RelationshipType.Child);
    path[^1].Generation.Should().Be(Generation.Zero);
    path[^1].Consanguinity.Should().Be(Consanguinity.UncleAunt);
  }

  [Fact]
  public async Task FindPathAsync_Spouse_ReturnsSingleHop()
  {
    var personA = _documentMock.CreatePerson();
    var personB = _documentMock.CreatePerson();
    _documentMock.AddRelationship(personA, personB, RelationshipType.Spouse);

    var path = await Finder.FindPathAsync(personA, personB, CancellationToken.None);

    path.Should().NotBeNull();
    path!.Should().ContainSingle();
    path[0].Type.Should().Be(RelationshipType.Spouse);
  }

  [Fact]
  public async Task FindPathAsync_SpousesParent_ReturnsInLawHop()
  {
    var self = _documentMock.CreatePerson(BiologicalSex.Male);
    var wife = _documentMock.CreatePerson(BiologicalSex.Female);
    var wifesParent = _documentMock.CreatePerson();
    _documentMock.AddRelationship(self, wife, RelationshipType.Spouse);
    _documentMock.AddRelationship(wife, wifesParent, RelationshipType.Parent);

    var path = await Finder.FindPathAsync(self, wifesParent, CancellationToken.None);

    path.Should().NotBeNull();
    path![^1].Id.Should().Be(wifesParent.Id);
    path[^1].Type.Should().Be(RelationshipType.WifeParent);
  }

  [Fact]
  public async Task FindPathAsync_SpousesSibling_ReturnsNull()
  {
    // Known gap: RelativesProvider's in-law expansion reaches a spouse's parents but not their
    // siblings, so this reports "no relationship found" even though the two share a household.
    var self = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();
    var spousesParent = _documentMock.CreatePerson();
    var spousesSibling = _documentMock.CreatePerson();
    _documentMock.AddRelationship(self, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(spouse, spousesParent, RelationshipType.Parent);
    _documentMock.AddRelationship(spousesSibling, spousesParent, RelationshipType.Parent);

    var path = await Finder.FindPathAsync(self, spousesSibling, CancellationToken.None);

    path.Should().BeNull();
  }

  [Fact]
  public async Task FindPathAsync_Unrelated_ReturnsNull()
  {
    var personA = _documentMock.CreatePerson();
    var personB = _documentMock.CreatePerson();

    var path = await Finder.FindPathAsync(personA, personB, CancellationToken.None);

    path.Should().BeNull();
  }
}
