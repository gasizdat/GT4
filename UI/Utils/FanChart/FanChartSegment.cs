using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

/// <summary>
/// One ancestor's ring sector. Angles are bearings in degrees, clockwise from straight up.
/// </summary>
/// <param name="Slot">The Ahnentafel number: the centre is 1, a slot k's father 2k and mother 2k + 1.</param>
/// <param name="Generation">The ring, counted from the centre. Taken from the slot, never from the node.</param>
/// <param name="IsRepeat">
/// The person already holds an earlier slot (pedigree collapse), so this one is only marked and their
/// ancestors are not drawn again behind it.
/// </param>
/// <param name="LabelCenter">Where the label's centre lands on the canvas.</param>
/// <param name="LabelRotation">Clockwise, in (−90°, 90°], so the text never reads upside down.</param>
/// <param name="LabelSize">In the label's own unrotated frame: the width runs along the text.</param>
public sealed record FanChartSegment(
  int Slot,
  int Generation,
  FamilyTreeNode Node,
  bool IsRepeat,
  double StartAngle,
  double SweepAngle,
  double InnerRadius,
  double OuterRadius,
  Point LabelCenter,
  double LabelRotation,
  Size LabelSize,
  FanChartLabel Label);
