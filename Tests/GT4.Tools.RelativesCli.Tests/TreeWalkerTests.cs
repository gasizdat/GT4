using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Moq;
using Xunit;

namespace GT4.Tools.RelativesCli.Tests;

/// <summary>
/// Pins the walk order, depth/path bookkeeping and Loop vs. MultipleConnections handling of
/// <see cref="TreeWalker"/>. The classification rule itself (same Generation = MultipleConnections)
/// is already covered by RelativeInfoExtensionsTests in GT4.Core.Project.Tests.
/// </summary>
public sealed class TreeWalkerTests
{
  private static readonly CancellationToken Token = CancellationToken.None;

  private readonly Mock<IRelativesProvider> _provider = new(MockBehavior.Strict);

  [Fact]
  public async Task Walk_FlattensPreOrder_WithDepthsAndPaths()
  {
    var root = MakeRelative(1, "Root", generation: 0);
    var child1 = MakeRelative(2, "First", generation: 1);
    var child2 = MakeRelative(3, "Second", generation: 1);
    var grandchild = MakeRelative(4, "Grand", generation: 2);
    SetupChildren(root, child1, child2);
    SetupChildren(child1, grandchild);
    SetupChildren(child2);
    SetupChildren(grandchild);

    var walker = new TreeWalker(_provider.Object);
    var (nodes, issues) = await walker.WalkAsync([root], Token);

    issues.Should().BeEmpty();
    nodes.Select(n => n.Info.DisplayName).Should().Equal("Root", "First", "Grand", "Second");
    nodes.Select(n => n.Depth).Should().Equal(0, 1, 2, 1);
    nodes.Select(n => n.Issue).Should().AllBeEquivalentTo(TreeIssueType.None);
    nodes[2].Path.Should().Be("Root -> First (Parent) -> Grand (Parent)");
  }

  [Fact]
  public async Task Walk_MultipleRoots_AllStartAtDepthZero()
  {
    var rootA = MakeRelative(1, "Alice", generation: 0);
    var rootB = MakeRelative(2, "Bob", generation: 0);
    SetupChildren(rootA);
    SetupChildren(rootB);

    var walker = new TreeWalker(_provider.Object);
    var (nodes, issues) = await walker.WalkAsync([rootA, rootB], Token);

    issues.Should().BeEmpty();
    nodes.Select(n => n.Depth).Should().Equal(0, 0);
    nodes.Select(n => n.Path).Should().Equal("Alice", "Bob");
  }

  [Fact]
  public async Task Walk_RevisitAtSameGeneration_FlagsMultipleConnections_AndStillExpands()
  {
    var root = MakeRelative(1, "Root", generation: 0);
    var left = MakeRelative(2, "Left", generation: 1);
    var right = MakeRelative(3, "Right", generation: 1);
    var sharedFirst = MakeRelative(4, "Shared", generation: 2, consanguinity: 1);
    var sharedSecond = MakeRelative(4, "Shared", generation: 2, consanguinity: 2);
    SetupChildren(root, left, right);
    SetupChildren(left, sharedFirst);
    SetupChildren(sharedFirst);
    SetupChildren(right, sharedSecond);
    SetupChildren(sharedSecond);

    var walker = new TreeWalker(_provider.Object);
    var (nodes, issues) = await walker.WalkAsync([root], Token);

    var revisited = nodes.Single(n => n.Info == sharedSecond);
    revisited.Issue.Should().Be(TreeIssueType.MultipleConnections);
    nodes.Where(n => n != revisited).Should().OnlyContain(n => n.Issue == TreeIssueType.None);

    var issue = issues.Should().ContainSingle().Subject;
    issue.Type.Should().Be(TreeIssueType.MultipleConnections);
    issue.PersonId.Should().Be(4);
    issue.FirstPath.Should().Be("Root -> Left (Parent) -> Shared (Parent)");
    issue.SecondPath.Should().Be("Root -> Right (Parent) -> Shared (Parent)");
    issue.FirstConsanguinity.Should().Be(new Consanguinity(1));
    issue.SecondConsanguinity.Should().Be(new Consanguinity(2));

    _provider.Verify(p => p.GetRelativeInfosAsync(sharedSecond, false, Token), Times.Once());
  }

  [Fact]
  public async Task Walk_RevisitAtDifferentGeneration_FlagsLoop_AndDoesNotExpand()
  {
    var root = MakeRelative(1, "Root", generation: 0);
    var left = MakeRelative(2, "Left", generation: 1);
    var right = MakeRelative(3, "Right", generation: 1);
    var sharedFirst = MakeRelative(4, "Shared", generation: 2);
    var sharedLooped = MakeRelative(4, "Shared", generation: 0);
    var after = MakeRelative(5, "After", generation: 2);
    SetupChildren(root, left, right);
    SetupChildren(left, sharedFirst);
    SetupChildren(sharedFirst);
    SetupChildren(right, sharedLooped, after);
    SetupChildren(after);

    var walker = new TreeWalker(_provider.Object);
    var (nodes, issues) = await walker.WalkAsync([root], Token);

    var looped = nodes.Single(n => n.Info == sharedLooped);
    looped.Issue.Should().Be(TreeIssueType.Loop);

    var issue = issues.Should().ContainSingle().Subject;
    issue.Type.Should().Be(TreeIssueType.Loop);
    issue.FirstGeneration.Should().Be(new Generation(2));
    issue.SecondGeneration.Should().Be(new Generation(0));

    // The loop must stop the walk on that branch but not on its siblings.
    _provider.Verify(p => p.GetRelativeInfosAsync(sharedLooped, false, Token), Times.Never());
    _provider.Verify(p => p.GetRelativeInfosAsync(after, false, Token), Times.Once());
  }

  private static RelativeInfo MakeRelative(int id, string name, int generation, int consanguinity = 0) =>
    new RelativeInfo(
      Id: id,
      BirthDate: Date.Now,
      DeathDate: null,
      BiologicalSex: BiologicalSex.Male,
      Names: [new Name(id, name, NameType.FirstName, null)],
      MainPhoto: null,
      Type: RelationshipType.Parent,
      Date: null,
      Generation: new Generation(generation),
      Consanguinity: new Consanguinity(consanguinity));

  private void SetupChildren(RelativeInfo parent, params RelativeInfo[] children) =>
    _provider
      .Setup(p => p.GetRelativeInfosAsync(parent, false, Token))
      .ReturnsAsync(children);
}
