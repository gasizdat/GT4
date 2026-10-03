namespace GT4.UI.Utils.Genealogy;

// A horizontal run in the band below a row, waiting for the track it is drawn on.
internal sealed class Run(int band, double from, double to)
{
  public int Band { get; } = band;
  public double Left { get; } = Math.Min(from, to);
  public double Right { get; } = Math.Max(from, to);
  public double Y { get; set; }
}
