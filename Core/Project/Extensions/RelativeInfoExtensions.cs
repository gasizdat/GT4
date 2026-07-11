namespace GT4.Core.Project.Extensions;

using GT4.Core.Project.Dto;

public static class RelativeInfoExtensions
{
  /// <summary>
  /// True when <paramref name="revisited"/> being seen again during tree expansion is a legitimate
  /// MultipleConnections case, not a Loop. <see cref="Generation"/> is a signed Parent(+1)/Child(-1)
  /// hop count, so it is path-independent for any internally consistent family graph -- it always
  /// comes out the same no matter which branch reached the person. <see cref="Consanguinity"/> is NOT
  /// path-independent (it also counts collateral/sideways distance), so an unequal-degree cousin
  /// marriage can legitimately give the same person two different Consanguinity values with zero
  /// contradiction in the data -- comparing it here would misreport that as a Loop.
  /// </summary>
  public static bool IsMultipleConnectionsOf(this RelativeInfo revisited, RelativeInfo firstSighting) =>
    revisited.Generation == firstSighting.Generation;
}
