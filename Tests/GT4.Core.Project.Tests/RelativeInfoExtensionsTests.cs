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
  private static RelativeInfo MakeRelative(Generation generation, Consanguinity consanguinity) =>
    new RelativeInfo(
      Id: 1,
      BirthDate: Date.Now,
      DeathDate: null,
      BiologicalSex: BiologicalSex.Male,
      Names: [],
      MainPhoto: null,
      Type: RelationshipType.Parent,
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
}
