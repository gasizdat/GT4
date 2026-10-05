using FluentAssertions;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.HtmlExport;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public sealed class RelativesWalkerTests
{
  // Each relative's children by id; anyone not listed has none.
  private readonly Dictionary<int, RelativeInfo[]> _Children = [];

  private static RelativeInfo Relative(int id, int generation = 0, BiologicalSex sex = BiologicalSex.Male) =>
    new(
      Id: id,
      BirthDate: Date.Now,
      DeathDate: null,
      BiologicalSex: sex,
      Names: [],
      MainPhoto: null,
      Type: RelationshipType.Child,
      Date: null,
      Generation: new Generation(generation),
      Consanguinity: Consanguinity.Zero);

  private Task<(RelativesWalkRow[] Rows, bool IsTruncated)> WalkAsync(RelativeInfo[] roots, int maxRows = 100)
  {
    var provider = new Mock<IRelativesProvider>();
    provider
      .Setup(p => p.GetRelativeInfosAsync(It.IsAny<RelativeInfo>(), false, It.IsAny<CancellationToken>()))
      .ReturnsAsync((RelativeInfo relative, bool _, CancellationToken _) => _Children.GetValueOrDefault(relative.Id, []));
    var walker = new RelativesWalker(provider.Object);
    return walker.WalkAsync(roots, maxRows, CancellationToken.None);
  }

  [Fact]
  public async Task Children_ComeOutOrderedBySex()
  {
    _Children[1] = [Relative(2, sex: BiologicalSex.Female), Relative(3, sex: BiologicalSex.Male)];

    var (rows, _) = await WalkAsync([Relative(1)]);

    rows.Select(row => row.Relative.Id).Should().Equal(1, 3, 2);
  }

  [Fact]
  public async Task Rows_AreListedInTreeOrderWithTheirParentAndDepth()
  {
    var first = Relative(1);
    var second = Relative(2);
    var grandchild = Relative(3, -1);
    _Children[1] = [grandchild];

    var (rows, _) = await WalkAsync([first, second]);

    rows.Select(row => (row.Relative.Id, row.Parent?.Id, row.Depth)).Should().Equal(
      (1, null, 0),
      (3, 1, 1),
      (2, null, 0));
  }

  // Depth-first, the person would be met first under 1's deep branch and expanded there.
  [Fact]
  public async Task AShallowerRow_IsExpandedBeforeADeeperOne()
  {
    _Children[1] = [Relative(10, -1)];
    _Children[10] = [Relative(5, -1)];
    _Children[5] = [Relative(6, -2)];

    var (rows, _) = await WalkAsync([Relative(1), Relative(5)]);

    var expanded = rows.Single(row => row.Relative.Id == 5 && row.Issue == RelativeIssue.None);
    var repeat = rows.Single(row => row.Relative.Id == 5 && row.Issue != RelativeIssue.None);
    expanded.Depth.Should().Be(0);
    repeat.Depth.Should().Be(2);
    rows.Single(row => row.Relative.Id == 6).Parent!.Should().BeSameAs(expanded.Relative);
  }

  [Fact]
  public async Task APersonReachedAgain_IsFlaggedAndNotExpanded()
  {
    _Children[1] = [Relative(3, -1)];
    _Children[2] = [Relative(3, -1)];
    _Children[3] = [Relative(4, -2)];

    var (rows, _) = await WalkAsync([Relative(1), Relative(2)]);

    var repeat = rows.Last(row => row.Relative.Id == 3);
    repeat.Issue.Should().Be(RelativeIssue.MultipleConnections);
    rows.Should().ContainSingle(row => row.Relative.Id == 4);
    rows.Single(row => row.Relative.Id == 4).Parent!.Should().NotBeSameAs(repeat.Relative);
  }

  [Fact]
  public async Task ALoopRow_IsNotExpanded()
  {
    _Children[1] = [Relative(2, 1)];
    _Children[2] = [Relative(1, -1)];

    var (rows, isTruncated) = await WalkAsync([Relative(1)]);

    rows.Select(row => (row.Relative.Id, row.Issue)).Should().Equal(
      (1, RelativeIssue.None),
      (2, RelativeIssue.None),
      (1, RelativeIssue.Loop));
    isTruncated.Should().BeFalse();
  }

  [Fact]
  public async Task TheWalk_StopsAtTheCapAndSaysSo()
  {
    _Children[1] = [Relative(2, -1), Relative(3, -1), Relative(4, -1)];

    var (rows, isTruncated) = await WalkAsync([Relative(1)], maxRows: 3);

    rows.Should().HaveCount(3);
    isTruncated.Should().BeTrue();
  }

  [Fact]
  public async Task MoreRootsThanTheCap_AreTruncated()
  {
    var (rows, isTruncated) = await WalkAsync([Relative(1), Relative(2), Relative(3)], maxRows: 2);

    rows.Select(row => row.Relative.Id).Should().Equal(1, 2);
    isTruncated.Should().BeTrue();
  }

  [Fact]
  public async Task AWalkThatFillsTheCapExactly_IsNotTruncated()
  {
    _Children[1] = [Relative(2, -1), Relative(3, -1)];

    var (rows, isTruncated) = await WalkAsync([Relative(1)], maxRows: 3);

    rows.Should().HaveCount(3);
    isTruncated.Should().BeFalse();
  }

  // Flagged as listed, so a repeat still waiting to be expanded when the cap falls is flagged anyway.
  [Fact]
  public async Task ARepeatListedBeforeTheCap_IsFlagged()
  {
    _Children[1] = [Relative(2, -1), Relative(2, -1), Relative(3, -1)];
    _Children[2] = [Relative(4, -2)];

    var (rows, isTruncated) = await WalkAsync([Relative(1)], maxRows: 3);

    isTruncated.Should().BeTrue();
    rows[2].Relative.Id.Should().Be(2);
    rows[2].Issue.Should().Be(RelativeIssue.MultipleConnections);
  }
}
