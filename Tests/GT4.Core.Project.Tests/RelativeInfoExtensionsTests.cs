using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// Covers the Loop vs. MultipleConnections classification shared by
/// <c>UI.App.Components.RelativeTree</c> and <c>Tools.RelativesCli</c>'s tree walker.
/// </summary>
public sealed class RelativeInfoExtensionsTests
{
  private static RelativeInfo MakeRelative(
    Generation generation,
    Consanguinity consanguinity,
    RelationshipType type = RelationshipType.Parent) =>
    new RelativeInfo(
      Id: 1,
      BirthDate: Date.Now,
      DeathDate: null,
      BiologicalSex: BiologicalSex.Male,
      Names: [],
      MainPhoto: null,
      Type: type,
      Date: null,
      Generation: generation,
      Consanguinity: consanguinity);

  [Fact]
  public void SameGeneration_IsMultipleConnections_RegardlessOfConsanguinity()
  {
    // A cousin marriage can legitimately reach the same person twice at the same Generation but
    // different Consanguinity (unequal-degree cousins) -- must not be reported as a Loop.
    var firstSighting = MakeRelative(Generation.Parent, Consanguinity.Sibling);
    var revisited = MakeRelative(Generation.Parent, Consanguinity.UncleAunt);

    revisited.IsMultipleConnectionsOf(firstSighting).Should().BeTrue();
  }

  [Fact]
  public void DifferentGeneration_IsLoop()
  {
    var firstSighting = MakeRelative(Generation.Parent, Consanguinity.Sibling);
    var revisited = MakeRelative(Generation.Child, Consanguinity.Sibling);

    revisited.IsMultipleConnectionsOf(firstSighting).Should().BeFalse();
  }

  [Theory]
  [InlineData(1, 0, RelationshipType.Parent, 0.5)]
  [InlineData(-1, 0, RelationshipType.Child, 0.5)]
  [InlineData(2, 0, RelationshipType.Parent, 0.25)]
  [InlineData(3, 0, RelationshipType.Parent, 0.125)]
  [InlineData(0, 1, RelationshipType.Sibling, 0.5)]
  [InlineData(0, 1, RelationshipType.SiblingByMother, 0.25)]
  [InlineData(0, 1, RelationshipType.SiblingByFather, 0.25)]
  [InlineData(1, 2, RelationshipType.Sibling, 0.25)] // uncle/aunt
  [InlineData(-1, 1, RelationshipType.Sibling, 0.25)] // niece/nephew
  [InlineData(0, 2, RelationshipType.Child, 0.125)] // first cousin
  [InlineData(-1, 2, RelationshipType.Child, 0.0625)] // first cousin, once removed
  [InlineData(2, 3, RelationshipType.Sibling, 0.125)] // great-uncle/aunt
  public void GetBloodShare_matches_the_expected_coefficient(
    int generation, int consanguinity, RelationshipType type, double expected)
  {
    var relative = MakeRelative(new Generation(generation), new Consanguinity(consanguinity), type);

    relative.GetBloodShare().Should().BeApproximately(expected, 0.0001);
  }

  [Theory]
  [InlineData(RelationshipType.Spouse)]
  [InlineData(RelationshipType.AdoptiveParent)]
  [InlineData(RelationshipType.AdoptiveChild)]
  [InlineData(RelationshipType.AdoptiveSibling)]
  [InlineData(RelationshipType.StepParent)]
  [InlineData(RelationshipType.StepChild)]
  [InlineData(RelationshipType.StepSibling)]
  [InlineData(RelationshipType.SpouseParent)]
  [InlineData(RelationshipType.SpouseSibling)]
  [InlineData(RelationshipType.HusbandParent)]
  [InlineData(RelationshipType.HusbandSibling)]
  [InlineData(RelationshipType.WifeParent)]
  [InlineData(RelationshipType.WifeSibling)]
  public void GetBloodShare_is_null_for_non_blood_relations(RelationshipType type)
  {
    var relative = MakeRelative(Generation.Zero, Consanguinity.Zero, type);

    relative.GetBloodShare().Should().BeNull();
  }
}
