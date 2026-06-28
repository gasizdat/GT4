using FluentAssertions;
using GT4.Core.Project.Dto;
using Xunit;

namespace GT4.Core.Project.Tests;

public sealed class RelativesProviderPerformanceTests
{
  private readonly ProjectDocumentMock _mock = new();
  private CancellationToken Token => TestContext.Current.CancellationToken;

  private RelativeInfo AsParentRelativeInfo(PersonFullInfo person) =>
    new(person, RelationshipType.Parent, null, Generation.Parent, Consanguinity.Zero);

  // Family:
  //   GrandFather (GF) + GrandMother (GM) → children: Father, Uncle
  //   Father + Mother → children: PersonA, Sibling
  private (
    PersonFullInfo gf, PersonFullInfo gm, PersonFullInfo uncle,
    PersonFullInfo father, PersonFullInfo mother,
    PersonFullInfo personA, PersonFullInfo sibling
  ) BuildFamily()
  {
    var gf = _mock.CreatePerson(BiologicalSex.Male);
    var gm = _mock.CreatePerson(BiologicalSex.Female);
    var uncle = _mock.CreatePerson();
    var father = _mock.CreatePerson(BiologicalSex.Male);
    var mother = _mock.CreatePerson(BiologicalSex.Female);
    var personA = _mock.CreatePerson();
    var sibling = _mock.CreatePerson();

    _mock.AddRelationship(gf, father, RelationshipType.Child);
    _mock.AddRelationship(gf, uncle, RelationshipType.Child);
    _mock.AddRelationship(gm, father, RelationshipType.Child);
    _mock.AddRelationship(gm, uncle, RelationshipType.Child);
    _mock.AddRelationship(father, mother, RelationshipType.Spouse);
    _mock.AddRelationship(father, personA, RelationshipType.Child);
    _mock.AddRelationship(father, sibling, RelationshipType.Child);
    _mock.AddRelationship(mother, personA, RelationshipType.Child);
    _mock.AddRelationship(mother, sibling, RelationshipType.Child);

    return (gf, gm, uncle, father, mother, personA, sibling);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_ExpandParent_NeverCallsGetPersonFullInfo()
  {
    var (gf, gm, uncle, father, _, personA, _) = BuildFamily();
    var provider = new RelativesProvider(_mock);
    _mock.ResetCallCounts();

    var result = await provider.GetRelativeInfosAsync(AsParentRelativeInfo(father), MainPhoto.Reference, Token);

    _mock.GetPersonFullInfoCallCount
      .Should().Be(0, "sibling lookup must use GetRelativesAsync, not GetPersonFullInfoAsync");

    result.Id().Should().Contain(gf.Id);
    result.Id().Should().Contain(gm.Id);
    result.Id().Should().Contain(uncle.Id);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_ExpandParent_RelativesCallCountEqualsTwoParentsOneSelf()
  {
    // GetRelativesAsync should be called once for the expanded relative (Father)
    // and once for each of Father's parents (GF, GM) — total 3.
    var (_, _, _, father, _, _, _) = BuildFamily();
    var provider = new RelativesProvider(_mock);
    _mock.ResetCallCounts();

    await provider.GetRelativeInfosAsync(AsParentRelativeInfo(father), MainPhoto.Reference, Token);

    _mock.GetRelativesCallCount
      .Should().Be(3, "1 for Father + 1 for GF + 1 for GM");
  }

  [Fact]
  public async Task GetRelativeInfosAsync_ExpandParent_PersonInfosCallCountIsMinimal()
  {
    // After deduplication of siblings across parents, GetPersonInfosAsync(Person[]) should
    // be called only twice: once for Father's filtered direct relatives (GF, GM),
    // and once for the deduplicated siblings (Uncle appears in both GF and GM's children).
    var (_, _, _, father, _, _, _) = BuildFamily();
    var provider = new RelativesProvider(_mock);
    _mock.ResetCallCounts();

    await provider.GetRelativeInfosAsync(AsParentRelativeInfo(father), MainPhoto.Reference, Token);

    _mock.GetPersonInfosWithPersonsCallCount
      .Should().Be(2, "1 call for direct relatives + 1 call for deduplicated siblings");
  }

  [Fact]
  public async Task GetRelativeInfosAsync_ExpandParent_ReturnsCorrectRelatives()
  {
    var (gf, gm, uncle, father, _, _, _) = BuildFamily();
    var provider = new RelativesProvider(_mock);

    var result = await provider.GetRelativeInfosAsync(AsParentRelativeInfo(father), MainPhoto.Reference, Token);

    result.Id().Should().BeEquivalentTo([gf.Id, gm.Id, uncle.Id]);

    var rGf = result.SingleId(gf);
    rGf.Type.Should().Be(RelationshipType.Parent);
    rGf.Generation.Value.Should().Be(2);

    var rUncle = result.SingleId(uncle);
    rUncle.Type.Should().Be(RelationshipType.Sibling);
    rUncle.Generation.Should().Be(Generation.Parent);
    rUncle.Consanguinity.Should().Be(Consanguinity.UncleAunt);
  }

  [Fact]
  public async Task GetRelativeInfosAsync_ExpandParentWithNoSiblings_PersonInfosCalledOnce()
  {
    // When the parent has no siblings (GF has only one child), sibling loading
    // must not issue an empty GetPersonInfosAsync call.
    var gf = _mock.CreatePerson(BiologicalSex.Male);
    var father = _mock.CreatePerson(BiologicalSex.Male);
    var personA = _mock.CreatePerson();

    _mock.AddRelationship(gf, father, RelationshipType.Child);
    _mock.AddRelationship(father, personA, RelationshipType.Child);

    var provider = new RelativesProvider(_mock);
    _mock.ResetCallCounts();

    await provider.GetRelativeInfosAsync(AsParentRelativeInfo(father), MainPhoto.Reference, Token);

    _mock.GetPersonInfosWithPersonsCallCount
      .Should().Be(1, "only the direct relatives batch; no siblings to load");
    _mock.GetPersonFullInfoCallCount
      .Should().Be(0);
  }
}
