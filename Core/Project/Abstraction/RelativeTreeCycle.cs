namespace GT4.Core.Project.Abstraction;

using GT4.Core.Project.Dto;

/// <summary>
/// Shared by <c>UI.App.Components.RelativeTree</c> (the app's expand-all-relatives view) and
/// <c>Tools.RelativesCli</c>'s tree walker: both expand a relatives tree and must tell a genuine
/// relationship-graph loop apart from a person legitimately reached twice (e.g. a cousin marriage
/// sharing a great-grandparent).
/// </summary>
public static class RelativeTreeCycle
{
  /// <summary>
  /// True when a person revisited during tree expansion is a legitimate MultipleConnections case,
  /// not a Loop. <see cref="Generation"/> is a signed Parent(+1)/Child(-1) hop count, so it is
  /// path-independent for any internally consistent family graph -- it always comes out the same no
  /// matter which branch reached the person. <see cref="Consanguinity"/> is NOT path-independent (it
  /// also counts collateral/sideways distance), so an unequal-degree cousin marriage can legitimately
  /// give the same person two different Consanguinity values with zero contradiction in the data --
  /// comparing it here would misreport that as a Loop.
  /// </summary>
  public static bool IsMultipleConnections(RelativeInfo firstSighting, RelativeInfo revisited) =>
    firstSighting.Generation == revisited.Generation;
}
