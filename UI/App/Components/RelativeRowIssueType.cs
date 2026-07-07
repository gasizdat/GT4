namespace GT4.UI.Components;

/// <summary>
/// Flags a condition RelativeTree.ExpandAllAsync found while unfolding a row's ancestry, surfaced
/// inline on the RelativeRow itself instead of a one-off alert.
/// </summary>
public enum RelativeRowIssueType
{
  None,

  /// <summary>The same person reappeared with a different Generation/Consanguinity than the first
  /// time it was seen -- an actual relationship cycle (most likely corrupt GEDCOM data). The row is
  /// left collapsed and stays that way; it can't be expanded, manually or automatically.</summary>
  Loop,

  /// <summary>The same person reappeared with the same Generation/Consanguinity as the first time --
  /// a legitimate shared ancestor reached via two different branches (e.g. cousin marriage). Finite,
  /// informational only; the row keeps expanding normally.</summary>
  MultipleConnections
}
