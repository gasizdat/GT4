using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Unit coverage for the value-type DTOs and helpers: <see cref="Generation"/> /
/// <see cref="Consanguinity"/> arithmetic and ordering, <see cref="ElementIdComparer{T}"/>, and the
/// <see cref="RelativeInfo"/> -> <see cref="PersonInfo"/> projection.
/// </summary>
public sealed class DtoTests
{
  [Theory]
  [InlineData(RelationshipType.Parent, 1)]
  [InlineData(RelationshipType.AdoptiveParent, 1)]
  [InlineData(RelationshipType.Child, -1)]
  [InlineData(RelationshipType.AdoptiveChild, -1)]
  [InlineData(RelationshipType.Spouse, 0)]
  public void Generation_FromRelationship_MapsToOffset(RelationshipType type, int expected)
  {
    new Generation(type).Value.Should().Be(expected);
  }

  [Fact]
  public void Generation_FromUnsupportedRelationship_Throws()
  {
    var act = () => new Generation(RelationshipType.Sibling);

    act.Should().Throw<ApplicationException>();
  }

  [Fact]
  public void Generation_Arithmetic_AddsAndSubtracts()
  {
    (Generation.Parent + Generation.Parent).Value.Should().Be(2);
    (Generation.Parent - Generation.Child).Value.Should().Be(2);

    var inc = Generation.Zero;
    inc++;
    inc.Value.Should().Be(1);

    var dec = Generation.Zero;
    dec--;
    dec.Value.Should().Be(-1);
  }

  [Fact]
  public void Generation_Ordering_AndEquality()
  {
    (Generation.Child < Generation.Parent).Should().BeTrue();
    (Generation.Parent > Generation.Child).Should().BeTrue();
    (Generation.Zero <= new Generation(0)).Should().BeTrue();
    (Generation.Zero >= new Generation(0)).Should().BeTrue();
    Generation.Zero.CompareTo(Generation.Parent).Should().BeNegative();
    Generation.Parent.Equals(new Generation(1)).Should().BeTrue();
    Generation.Parent.GetHashCode().Should().Be(new Generation(1).GetHashCode());
  }

  [Fact]
  public void Consanguinity_Arithmetic_AndOrdering()
  {
    (Consanguinity.Sibling + Consanguinity.Sibling).Value.Should().Be(2);
    (Consanguinity.UncleAunt - Consanguinity.Sibling).Should().Be(Consanguinity.Sibling);

    var inc = Consanguinity.Zero;
    inc++;
    inc.Should().Be(Consanguinity.Sibling);

    var dec = Consanguinity.Sibling;
    dec--;
    dec.Should().Be(Consanguinity.Zero);

    (Consanguinity.Zero < Consanguinity.Sibling).Should().BeTrue();
    (Consanguinity.UncleAunt > Consanguinity.Sibling).Should().BeTrue();
    (Consanguinity.Sibling <= new Consanguinity(1)).Should().BeTrue();
    (Consanguinity.Sibling >= new Consanguinity(1)).Should().BeTrue();
    Consanguinity.Sibling.CompareTo(Consanguinity.UncleAunt).Should().BeNegative();
    Consanguinity.Sibling.Equals(new Consanguinity(1)).Should().BeTrue();
    Consanguinity.Sibling.GetHashCode().Should().Be(new Consanguinity(1).GetHashCode());
  }

  [Fact]
  public void ElementIdComparer_UsesIdForEqualityAndHash()
  {
    var comparer = new ElementIdComparer<Person>();
    var a = new Person(7, Date.Now, null, BiologicalSex.Male);
    var b = a with { BiologicalSex = BiologicalSex.Female };
    var c = a with { Id = 8 };

    comparer.Equals(a, b).Should().BeTrue();
    comparer.Equals(a, c).Should().BeFalse();
    comparer.Equals(null, null).Should().BeTrue();
    comparer.GetHashCode(a).Should().Be(7);
  }

  [Fact]
  public void RelativeInfo_ImplicitlyProjectsToPersonInfo()
  {
    var names = new[] { new Name(1, "Anna", NameType.FirstName | NameType.FemaleDeclension, null) };
    var relative = new RelativeInfo(
      Id: 5,
      BirthDate: Date.Now,
      DeathDate: null,
      BiologicalSex: BiologicalSex.Female,
      Names: names,
      MainPhoto: null,
      Type: RelationshipType.Child,
      Date: null,
      Generation: Generation.Child,
      Consanguinity: Consanguinity.Zero);

    PersonInfo person = relative;

    person.Id.Should().Be(5);
    person.Names.Should().BeSameAs(names);
    person.DisplayName.Should().Be("Anna");
    relative.DisplayName.Should().Be("Anna");
  }

  [Fact]
  public void RelativeInfo_ImplicitProjection_OfNull_IsNull()
  {
    RelativeInfo? relative = null;

    PersonInfo? person = relative;

    person.Should().BeNull();
  }

  [Fact]
  public void PersonFullInfo_Empty_HasNonCommittedIdAndNoData()
  {
    var empty = PersonFullInfo.Empty;

    empty.Id.Should().Be(TableBase.NonCommitedId);
    empty.Names.Should().BeEmpty();
    empty.AdditionalPhotos.Should().BeEmpty();
    empty.RelativeInfos.Should().BeEmpty();
    empty.MainPhoto.Should().BeNull();
    empty.Biography.Should().BeNull();
  }

  [Fact]
  public void ProjectInfo_RecordEquality_HoldsAndWithCopies()
  {
    var origin = new FileDescription(
      new DirectoryDescription(Environment.SpecialFolder.MyDocuments, ["p"]), "a.gt4", null);
    var info = new ProjectInfo("Name", "Desc", "rev1", origin);

    info.Should().Be(info with { });
    (info with { Revision = "rev2" }).Should().NotBe(info);
  }
}
