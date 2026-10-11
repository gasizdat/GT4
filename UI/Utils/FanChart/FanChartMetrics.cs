namespace GT4.UI.Utils.Genealogy;

/// <summary>Sizing knobs for <see cref="FanChartLayout"/>, all in device-independent units.</summary>
/// <param name="MinLineHeight">
/// How far a label's lines may shrink below <paramref name="LineHeight"/> to fit a thin segment.
/// </param>
public sealed record FanChartMetrics(
  double CenterRadius = 80,
  double RingWidth = 96,
  double LineHeight = 16,
  double MinLineHeight = 16,
  double Margin = 32);
