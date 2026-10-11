namespace GT4.UI.Utils.Genealogy;

/// <summary>Sizing knobs for <see cref="FanChartLayout"/>, all in device-independent units.</summary>
public sealed record FanChartMetrics(
  double CenterRadius = 80,
  double RingWidth = 96,
  double LineHeight = 16,
  double Margin = 32);
