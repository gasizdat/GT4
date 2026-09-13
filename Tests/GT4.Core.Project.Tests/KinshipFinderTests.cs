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
  public async Task FindPathAsync_SpousesSibling_ReturnsInLawHop()
  {
    // Formerly a documented gap (#393): RelativesProvider's in-law expansion reached a spouse's
    // parents but not their siblings, so this reported "no relationship found" even though the two
    // share a household -- and, one direction-dependent step further, made the finder give a
    // different answer depending on which of the pair you searched from. Now Spouse->Sibling is a
    // supported hop, remapped to *Sibling (Husband/Wife/Spouse) by the spouse's own sex, same as
    // Spouse->Parent already was.
    var self = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson();
    var spousesParent = _documentMock.CreatePerson();
    var spousesSibling = _documentMock.CreatePerson();
    _documentMock.AddRelationship(self, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(spouse, spousesParent, RelationshipType.Parent);
    _documentMock.AddRelationship(spousesSibling, spousesParent, RelationshipType.Parent);

    var path = await Finder.FindPathAsync(self, spousesSibling, CancellationToken.None);

    path.Should().NotBeNull();
    path!.Select(r => r.Id).Should().ContainInOrder(spouse.Id, spousesSibling.Id);
    path[^1].Type.Should().Be(RelationshipType.SpouseSibling);
  }

  [Fact]
  public async Task FindPathAsync_SpousesSiblingsGreatGrandchild_SymmetricInBothDirections()
  {
    // Reproduces #393 on real (non-synthetic) family data: person 41's spouse's sibling has
    // descendants three generations down, and only the blood-first direction (descendant ->
    // ... -> ancestor's sibling -> spouse) used to find them; the marriage-first direction
    // (spouse -> spouse's sibling -> ... -> descendant) dead-ended at the in-law-sibling hop.
    var spouseParent = _documentMock.CreatePerson();
    var spouse = _documentMock.CreatePerson(BiologicalSex.Female);
    var spouseSibling = _documentMock.CreatePerson(BiologicalSex.Male);
    var self = _documentMock.CreatePerson(BiologicalSex.Male);
    var child = _documentMock.CreatePerson();
    var grandchild = _documentMock.CreatePerson();
    var greatGrandchild = _documentMock.CreatePerson();
    _documentMock.AddRelationship(spouseParent, spouse, RelationshipType.Child);
    _documentMock.AddRelationship(spouseParent, spouseSibling, RelationshipType.Child);
    _documentMock.AddRelationship(self, spouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(spouseSibling, child, RelationshipType.Child);
    _documentMock.AddRelationship(child, grandchild, RelationshipType.Child);
    _documentMock.AddRelationship(grandchild, greatGrandchild, RelationshipType.Child);

    var forward = await Finder.FindPathAsync(self, greatGrandchild, CancellationToken.None);
    var reverse = await Finder.FindPathAsync(greatGrandchild, self, CancellationToken.None);

    forward.Should().NotBeNull();
    forward!.Select(r => r.Id).Should().ContainInOrder(spouse.Id, spouseSibling.Id, child.Id, grandchild.Id, greatGrandchild.Id);
    forward[1].Type.Should().Be(RelationshipType.WifeSibling);
    forward[^1].Generation.Should().Be(new Generation(-3));

    reverse.Should().NotBeNull();
    reverse!.Select(r => r.Id).Should().ContainInOrder(grandchild.Id, child.Id, spouseSibling.Id, spouse.Id, self.Id);
  }

  [Fact]
  public async Task FindPathAsync_AuntMarriesOwnNephew_SymmetricInBothDirections()
  {
    // Reproduces #393 with an ordinary (non-contradictory) tree -- an avuncular marriage, attested in
    // real genealogies, is enough: `auntOrUncle` marries her own nephew `nephewSpouse` (grandfather's
    // other child). Searching from `auntOrUncle`, her spouse root is enqueued before her sibling root
    // (GetRootsAsync order), and expanding a Spouse-typed node maps the spouse's own parent -- here
    // `grandfather` -- to an in-law type (HusbandParent/WifeParent), which IsRelationshipSupported
    // treats as a dead end. Old code's `visited` set is keyed by id alone, so that dead-end arrival at
    // `grandfather` blocks the later Sibling-typed arrival that could descend through `father` to
    // `subject` -- even though nothing about the tree itself is asymmetric.
    //
    // Since Spouse->Sibling became a supported hop, `reverse` now takes an even more direct route
    // (nephewSpouse's own sibling is `father`), which no longer exercises the id+type visited-set fix
    // this test was written for -- see FindPathAsync_SpousesSiblingsGreatGrandchild_SymmetricInBoth
    // Directions for a fixture that isolates that capability instead. This test still pins that both
    // directions land on the same, correct, shortest chain.
    var ggFather = _documentMock.CreatePerson(BiologicalSex.Male);
    var ggMother = _documentMock.CreatePerson(BiologicalSex.Female);
    var greatGrandmother = _documentMock.CreatePerson();
    var auntOrUncle = _documentMock.CreatePerson(BiologicalSex.Female);
    var grandfather = _documentMock.CreatePerson();
    var nephewSpouse = _documentMock.CreatePerson(BiologicalSex.Male);
    var father = _documentMock.CreatePerson();
    var subject = _documentMock.CreatePerson();
    _documentMock.AddRelationship(greatGrandmother, ggFather, RelationshipType.Parent);
    _documentMock.AddRelationship(greatGrandmother, ggMother, RelationshipType.Parent);
    _documentMock.AddRelationship(auntOrUncle, ggFather, RelationshipType.Parent);
    _documentMock.AddRelationship(auntOrUncle, ggMother, RelationshipType.Parent);
    _documentMock.AddRelationship(grandfather, greatGrandmother, RelationshipType.Parent);
    _documentMock.AddRelationship(nephewSpouse, grandfather, RelationshipType.Parent);
    _documentMock.AddRelationship(auntOrUncle, nephewSpouse, RelationshipType.Spouse);
    _documentMock.AddRelationship(father, grandfather, RelationshipType.Parent);
    _documentMock.AddRelationship(subject, father, RelationshipType.Parent);

    var forward = await Finder.FindPathAsync(subject, auntOrUncle, CancellationToken.None);
    var reverse = await Finder.FindPathAsync(auntOrUncle, subject, CancellationToken.None);

    forward.Should().NotBeNull();
    forward!.Select(r => r.Id).Should().ContainInOrder(father.Id, nephewSpouse.Id, auntOrUncle.Id);
    reverse.Should().NotBeNull();
    reverse!.Select(r => r.Id).Should().ContainInOrder(nephewSpouse.Id, father.Id, subject.Id);
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
