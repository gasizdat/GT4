namespace GT4.UI.Utils.Genealogy;

internal sealed class PixelGrid
{
  private readonly IReadOnlyDictionary<int, double> _X;
  private readonly FamilyTreeLayoutMetrics _Metrics;
  private readonly double _MinSlot;
  private readonly double _Origin;

  public PixelGrid(Families families, IReadOnlyDictionary<int, double> x, FamilyTreeLayoutMetrics metrics)
  {
    _X = x;
    _Metrics = metrics;
    _MinSlot = x.Values.Min();
    _Origin = Math.Ceiling(metrics.Margin + (metrics.NodeWidth / 2));
    MaxGeneration = families.Nodes.Values.Max(node => node.Generation);
  }

  public int MaxGeneration { get; }

  // The one place a slot becomes a pixel, so positions equal in slots stay equal on screen.
  public double Snap(double slot) => _Origin + Math.Round((slot - _MinSlot) * _Metrics.SlotPitch, MidpointRounding.AwayFromZero);

  public double CentreX(int id) => Snap(_X[id]);

  public double Top(int generation) => _Metrics.Margin + ((MaxGeneration - generation) * _Metrics.RowPitch);

  public double Bottom(int generation) => Top(generation) + _Metrics.NodeHeight;

  public double Middle(int generation) => Top(generation) + (_Metrics.NodeHeight / 2);
}
