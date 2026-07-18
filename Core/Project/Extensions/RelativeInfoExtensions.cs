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

  private static readonly HashSet<RelationshipType> _NonBloodTypes =
  [
    RelationshipType.Spouse,
    RelationshipType.AdoptiveParent,
    RelationshipType.AdoptiveChild,
    RelationshipType.AdoptiveSibling,
    RelationshipType.StepParent,
    RelationshipType.StepChild,
    RelationshipType.StepSibling,
    RelationshipType.SpouseParent,
    RelationshipType.SpouseSibling,
    RelationshipType.HusbandParent,
    RelationshipType.HusbandSibling,
    RelationshipType.WifeParent,
    RelationshipType.WifeSibling
  ];

  /// <summary>
  /// Coefficient of relationship (fraction of shared DNA), or <see langword="null"/> for a relation
  /// that carries no blood (spouse, adoptive, step, in-law). Direct line (<see cref="Consanguinity"/>
  /// zero) is <c>(1/2)^|Generation|</c>; a collateral relation is <c>nca * (1/2)^(2*Consanguinity -
  /// Generation)</c>, where <c>nca</c> is the number of shared common ancestors (2 for full-blood, 1
  /// for a half-sibling hop). Only a root-level <see cref="RelationshipType.SiblingByMother"/>/
  /// <see cref="RelationshipType.SiblingByFather"/> row carries the half-blood marker -- anything
  /// expanded further from it is approximated as full-blood, since the tree walk does not thread that
  /// distinction past one hop.
  /// </summary>
  public static double? GetBloodShare(this RelativeInfo relative)
  {
    if (_NonBloodTypes.Contains(relative.Type))
      return null;

    var generation = relative.Generation.Value;
    var consanguinity = relative.Consanguinity.Value;
    if (consanguinity == 0)
      return Math.Pow(0.5, Math.Abs(generation));

    var isHalfBlood = relative.Type is RelationshipType.SiblingByMother or RelationshipType.SiblingByFather;
    var sharedAncestors = isHalfBlood ? 1 : 2;
    return sharedAncestors * Math.Pow(0.5, 2 * consanguinity - generation);
  }
}
