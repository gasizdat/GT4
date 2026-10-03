using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

internal sealed class ConnectorRouter
{
  private readonly Families _Families;
  private readonly IReadOnlyDictionary<int, double> _X;
  private readonly IReadOnlyDictionary<int, string> _PlacedBy;
  private readonly PixelGrid _Grid;
  private readonly FamilyTreeLayoutMetrics _Metrics;
  private readonly ConnectorDraft _Draft = new();
  private readonly Dictionary<int, List<double>> _Attachments = [];
  private readonly Dictionary<(int Generation, double Column), int> _GapUses = [];
  private readonly HashSet<int> _Hosts = [];

  public ConnectorRouter(
    Families families,
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, string> placedBy,
    PixelGrid grid,
    FamilyTreeLayoutMetrics metrics)
  {
    _Families = families;
    _X = x;
    _PlacedBy = placedBy;
    _Grid = grid;
    _Metrics = metrics;
  }

  public FamilyTreeConnector[] Route()
  {
    foreach (var key in _Families.Keys)
      RouteFamily(key);

    foreach (var (a, b) in _Families.Spouses)
    {
      if (_Families.Generation(a) != _Families.Generation(b))
      {
        Loop(a, b, FamilyTreeRelation.Spouse);
        continue;
      }
      if (!_Families.IsClearBetween(_X, a, b, [a, b]))
      {
        Bridge(a, b, FamilyTreeRelation.Spouse, isLoop: false);
        continue;
      }
      // Centre to centre: the photos cover the ends, so the line reaches each photo however much wider
      // than the photo the node is.
      var (left, right) = _X[a] <= _X[b] ? (a, b) : (b, a);
      var y = _Grid.Middle(_Families.Generation(a));
      _Draft.AddLine(FamilyTreeRelation.Spouse, false, new(_Grid.CentreX(left), y), new(_Grid.CentreX(right), y));
    }

    foreach (var (parent, child) in _Families.OffRowParents)
      Loop(parent, child, FamilyTreeRelation.ParentChild);

    return _Draft.ToConnectors(_Grid.Bottom, _Metrics);
  }

  private void RouteFamily(string key)
  {
    var partners = _Families.Partners(key).OrderBy(p => _X[p]).ToArray();
    var children = _Families.ChildrenOf(key);
    var placed = children.Where(child => _PlacedBy.GetValueOrDefault(child) == key).OrderBy(child => _X[child]).ToArray();
    var generation = _Families.Generation(partners[0]);

    if (placed.Length != 0)
    {
      var together = partners.Length > 1 && _Families.IsClearBetween(_X, partners[0], partners[^1], partners);
      var married = partners.Zip(partners.Skip(1)).All(pair => _Families.AreSpouses(pair.First, pair.Second));
      (double X, double Top)[] drops;
      if (together && married)
      {
        var drop = (_X[partners[0]] + _X[partners[^1]]) / 2;
        var dropX = _Grid.Snap(drop);
        var onPartner = partners.Any(p => Math.Abs(_Grid.CentreX(p) - dropX) < _Metrics.NodeWidth / 2);
        drops = [(dropX, onPartner ? _Grid.Bottom(generation) : _Grid.Middle(generation))];
      }
      else
      {
        var mean = placed.Average(child => _X[child]);
        var host = partners.MinBy(p => (Math.Abs(_X[p] - mean), _X[p]));
        // Parents with no marriage on record each drop on their own: a line between them would claim one.
        var droppers = together ? partners : [host];
        var toward = _Grid.Snap(mean);
        var bottom = _Grid.Bottom(generation);
        // A second family hung from one person, as after a third marriage, drops beside the first.
        drops = [.. droppers.Select(p => (_Hosts.Add(p) ? _Grid.CentreX(p) : Attach(p, top: false, toward), bottom))];
        foreach (var partner in partners.Where(p => !droppers.Contains(p) && !_Families.AreSpouses(p, host)))
          Loop(partner, host, FamilyTreeRelation.ParentChild);
      }

      var childTop = _Grid.Top(generation - 1);
      var xs = placed.Select(_Grid.CentreX).ToArray();
      var tops = drops.ToDictionary(d => d.X, d => d.Top);
      var columns = tops.Keys.Union(xs).Order().ToArray();
      bool Through(double column) => tops.ContainsKey(column) && xs.Contains(column);

      foreach (var column in columns.Where(Through))
        _Draft.AddLine(FamilyTreeRelation.ParentChild, false, new(column, tops[column]), new(column, childTop));
      if (columns.Length > 1)
      {
        var (low, high) = (columns[0], columns[^1]);
        var bar = _Draft.AddRun(generation, low, high);
        Waypoint End(double column)
        {
          if (Through(column))
            return new(column, 0, bar);
          var y = tops.GetValueOrDefault(column, childTop);
          return new(column, y);
        }

        // The bar turns into its two end columns round a corner; every column between meets it at a T.
        var first = End(low);
        var last = End(high);
        _Draft.AddLine(FamilyTreeRelation.ParentChild, false, first, new(low, 0, bar), new(high, 0, bar), last);
        foreach (var column in columns[1..^1].Where(column => !Through(column)))
        {
          var end = End(column);
          _Draft.AddLine(FamilyTreeRelation.ParentChild, false, new(column, 0, bar), end);
        }
      }
    }

    foreach (var child in children.Except(placed))
    {
      var nearest = partners.MinBy(p => (Math.Abs(_X[p] - _X[child]), _X[p]));
      Loop(nearest, child, FamilyTreeRelation.ParentChild);
    }
  }

  // A loop, or a family beyond the first one dropping from the node, leaves it off its centre line,
  // which belongs to the node's own families, and off any column already attached in the same band, as
  // by a loop from a node straight above or below.
  private double Attach(int id, bool top, double toward)
  {
    var band = _Families.Generation(id) + (top ? 1 : 0);
    if (!_Attachments.TryGetValue(band, out var taken))
      _Attachments[band] = taken = [];
    var centre = _Grid.CentreX(id);
    var sign = toward >= centre ? 1 : -1;
    var columns = new[] { 0.25, -0.25, 0.375, -0.375, 0.125, -0.125 }
      .Select(reach => Math.Round(centre + (sign * reach * _Metrics.NodeWidth)))
      .ToArray();
    var column = columns.FirstOrDefault(c => !taken.Any(t => Math.Abs(t - c) < 1), columns[0]);
    taken.Add(column);
    return column;
  }

  // Where a vertical can cross a row: in a gap between two of its nodes, or past either end.
  private double CrossingColumn(int generation, double from, double to)
  {
    var centres = _Families.Row(generation).Select(_Grid.CentreX).Order().ToArray();
    if (centres.Length == 0)
      return from;
    var half = _Metrics.SlotPitch / 2;
    var candidates = centres
      .Zip(centres.Skip(1), (a, b) => (a + b) / 2)
      .Prepend(centres[0] - half)
      .Append(centres[^1] + half);
    var gap = candidates.MinBy(c => (Math.Abs(c - from) + Math.Abs(c - to), c));
    var uses = _GapUses.GetValueOrDefault((generation, gap));
    _GapUses[(generation, gap)] = uses + 1;
    // Off the gap's centre line, where a couple's drop may already run.
    var offset = (uses % 2 == 0 ? 1 : -1) * _Metrics.HorizontalGap / 4;
    return Math.Round(gap + offset);
  }

  // Over the row through the band above it, or below the top row through the band under it.
  private void Bridge(int a, int b, FamilyTreeRelation relation, bool isLoop)
  {
    var generation = _Families.Generation(a);
    var above = generation < _Grid.MaxGeneration;
    var y = above ? _Grid.Top(generation) : _Grid.Bottom(generation);
    var band = above ? generation + 1 : generation;
    var bx = _Grid.CentreX(b);
    var ax = Attach(a, above, bx);
    var ex = Attach(b, above, _Grid.CentreX(a));
    var run = _Draft.AddRun(band, ax, ex);
    _Draft.AddLine(relation, isLoop, new(ax, y), new(ax, 0, run), new(ex, 0, run), new(ex, y));
  }

  private void Loop(int a, int b, FamilyTreeRelation relation)
  {
    if (_Families.Generation(a) == _Families.Generation(b))
    {
      Bridge(a, b, relation, isLoop: true);
      return;
    }

    var (upper, lower) = _Families.Generation(a) > _Families.Generation(b) ? (a, b) : (b, a);
    var upperGeneration = _Families.Generation(upper);
    var lowerGeneration = _Families.Generation(lower);
    var start = Attach(upper, top: false, _Grid.CentreX(lower));
    var end = Attach(lower, top: true, _Grid.CentreX(upper));
    var points = new List<Waypoint> { new(start, _Grid.Bottom(upperGeneration)) };
    var current = start;
    for (var generation = upperGeneration - 1; generation >= lowerGeneration; generation--)
    {
      var next = generation == lowerGeneration ? end : CrossingColumn(generation, current, end);
      if (Math.Abs(next - current) >= 0.5)
      {
        var run = _Draft.AddRun(generation + 1, current, next);
        points.Add(new(current, 0, run));
        points.Add(new(next, 0, run));
      }
      current = next;
    }
    points.Add(new(end, _Grid.Top(lowerGeneration)));
    _Draft.AddLine(relation, true, [.. points]);
  }
}
