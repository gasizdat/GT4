using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Resources;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers RelativeTree.ExpandAllAsync's issue detection: a corrupt GEDCOM import can produce an
/// actual relationship cycle (a person listed as their own ancestor/descendant), which would
/// otherwise make "expand all" re-insert the same subtree forever. A person legitimately reached
/// twice via two different branches with the same Generation/Consanguinity (e.g. cousin marriage
/// sharing a great-grandparent) must not be confused with that and should keep expanding.
/// </summary>
public class RelativeTreeTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static RelativeInfo MakeRelative(int id, string firstName, Generation generation) =>
    new(
      new PersonInfo(id, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male,
        [N(id * 100, firstName, NameType.FirstName)], null),
      RelationshipType.Parent,
      null,
      generation,
      Consanguinity.Zero);

  private static RelativeTree CreateTree(TestServices services) =>
    new(services.CurrentProjectProvider.Object,
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.AlertService.Object);

  [Fact]
  public async Task ExpandAllAsync_terminates_and_folds_the_row_when_the_backend_returns_a_relationship_cycle()
  {
    var services = new TestServices();
    var loopy = MakeRelative(1, "Loopy", Generation.Parent);
    var other = MakeRelative(2, "Other", Generation.Parent + Generation.Parent);
    // Same person (id 1) as loopy, but at a deeper Generation -- exactly what going one more step
    // around an actual cycle produces, as opposed to reaching the same person again through a
    // second, equally-deep branch (see the MultipleConnections test below).
    var loopySecondSighting = MakeRelative(1, "Loopy", Generation.Parent + Generation.Parent + Generation.Parent);

    // A cycle: expanding Loopy returns Other, and expanding Other returns Loopy right back.
    services.RelativesProvider
      .Setup(r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == loopy.Id), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([other]);
    services.RelativesProvider
      .Setup(r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == other.Id), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([loopySecondSighting]);

    var tree = CreateTree(services);
    await MainThread.InvokeOnMainThreadAsync(() => tree.SetRoots([loopy], null));

    await tree.ExpandAllAsync(true);

    // Loopy (root) -> Other -> a second Loopy row, folded instead of re-expanded forever.
    Assert.Equal(3, tree.Rows.Count);
    var foldedLoopy = tree.Rows.Single(r => r.Relative.Id == loopy.Id && !r.IsExpanded);
    Assert.Equal(RelativeRowIssueType.Loop, foldedLoopy.Issue);
    Assert.Equal(UIStrings.HintRelativeLoopDetected, foldedLoopy.IssueMessage);
    Assert.False(foldedLoopy.IsExpanded);

    services.AlertService.Verify(a => a.ShowWarningAsync(It.IsAny<string>()), Times.Never());
    services.AlertService.Verify(a => a.ShowConfirmationAsync(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task ToggleAsync_on_a_folded_loop_row_does_not_re_expand_it()
  {
    var services = new TestServices();
    var loopy = MakeRelative(1, "Loopy", Generation.Parent);
    var other = MakeRelative(2, "Other", Generation.Parent + Generation.Parent);
    var loopySecondSighting = MakeRelative(1, "Loopy", Generation.Parent + Generation.Parent + Generation.Parent);
    services.RelativesProvider
      .Setup(r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == loopy.Id), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([other]);
    services.RelativesProvider
      .Setup(r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == other.Id), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([loopySecondSighting]);

    var tree = CreateTree(services);
    await MainThread.InvokeOnMainThreadAsync(() => tree.SetRoots([loopy], null));
    await tree.ExpandAllAsync(true);
    var foldedLoopy = tree.Rows.Single(r => r.Relative.Id == loopy.Id && !r.IsExpanded);
    var callsBefore = services.RelativesProvider.Invocations.Count;

    await tree.ToggleAsync(foldedLoopy);

    Assert.False(foldedLoopy.IsExpanded);
    Assert.Equal(callsBefore, services.RelativesProvider.Invocations.Count);
  }

  [Fact]
  public async Task ExpandAllAsync_flags_but_keeps_expanding_a_person_reached_through_two_branches()
  {
    var services = new TestServices();
    var rootA = MakeRelative(1, "RootA", Generation.Parent);
    var rootB = MakeRelative(2, "RootB", Generation.Parent);
    var shared = MakeRelative(3, "Shared", Generation.Parent + Generation.Parent);

    // Both roots' ancestry reaches the same person -- a shared great-grandparent from e.g. a cousin
    // marriage -- always at the same Generation/Consanguinity, so it's not a cycle.
    services.RelativesProvider
      .Setup(r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == rootA.Id), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([shared]);
    services.RelativesProvider
      .Setup(r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == rootB.Id), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([shared]);
    services.RelativesProvider
      .Setup(r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == shared.Id), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var tree = CreateTree(services);
    await MainThread.InvokeOnMainThreadAsync(() => tree.SetRoots([rootA, rootB], null));

    await tree.ExpandAllAsync(true);

    var sharedRows = tree.Rows.Where(r => r.Relative.Id == shared.Id).ToArray();
    Assert.Equal(2, sharedRows.Length);
    Assert.All(sharedRows, row => Assert.True(row.IsExpanded));
    Assert.Contains(sharedRows, row => row.Issue == RelativeRowIssueType.MultipleConnections);
    var flagged = sharedRows.Single(row => row.Issue == RelativeRowIssueType.MultipleConnections);
    Assert.Equal(UIStrings.HintRelativeMultipleConnections, flagged.IssueMessage);

    services.RelativesProvider.Verify(
      r => r.GetRelativeInfosAsync(It.Is<RelativeInfo>(x => x.Id == shared.Id), true, It.IsAny<CancellationToken>()),
      Times.Exactly(2));
  }
}
