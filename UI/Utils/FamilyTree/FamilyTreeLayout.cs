using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

/// <summary>
/// Turns a <see cref="FamilyTree"/> into node rectangles and pedigree-chart connectors.
/// <para>
/// Children are grouped into families by the parents they have in the row above. Positions come from a
/// tidy, contour-packed layout in slots (1 slot = <see cref="FamilyTreeLayoutMetrics.SlotPitch"/>): each
/// subtree packs beside its neighbours as closely as their row-by-row extents allow, a family's children
/// are centred under its drop, and each person on the direct line sits exactly under their parents' drop.
/// The father's relatives go outboard to the left, the mother's to the right. Each person is placed once,
/// so a relationship the placement cannot hold, as with pedigree collapse, is drawn as a loop.
/// </para>
/// <para>
/// Each family is drawn once: a marriage line with one drop from its midpoint, or a drop from each parent
/// when they have no marriage on record, then one sibship bar and a stub per child. Horizontal runs that
/// would overlap in the band between two rows get separate tracks there.
/// </para>
/// </summary>
public static class FamilyTreeLayout
{
  // Absorbs the rounding in a neighbouring slot computed as taken ± 1.
  internal const double SlotTolerance = 1e-9;

  /// <param name="pins">
  /// Slot offsets from the centre person for nodes the user placed by hand. When any apply, the centre
  /// stays where the layout put it, and each pinned node keeps its offset unless the centre or an earlier
  /// pin in its row already holds that slot, in which case it takes the nearest clear one. Every other
  /// node keeps its order and moves only as far as it must to stay a slot clear of the pins.
  /// </param>
  public static FamilyTreeLayoutResult Compute(
    FamilyTree tree,
    FamilyTreeLayoutMetrics metrics,
    IReadOnlyDictionary<int, double>? pins = null)
  {
    ArgumentNullException.ThrowIfNull(tree);
    ArgumentNullException.ThrowIfNull(metrics);

    if (tree.Nodes.Count == 0)
      return new FamilyTreeLayoutResult([], [], new Size(0, 0), new Point(0, 0), metrics);

    var families = new Families(tree);
    var placement = Placement.Compute(families, tree.CenterId);

    var x = placement.X;
    var pinned = Pins.Resolve(tree.CenterId, families, x, pins);
    if (pinned.Count != 0)
    {
      foreach (var (id, slot) in pinned)
        x[id] = slot;
      Pins.Clear(families, x, pinned);
    }

    return Assemble(tree.CenterId, families, x, placement.PlacedBy, metrics);
  }

  /// <summary>
  /// The slot nearest <paramref name="slot"/>, no lower than <paramref name="minimum"/>, that keeps a
  /// whole slot clear of every <paramref name="taken"/> one.
  /// </summary>
  public static double NearestClearSlot(double slot, double[] taken, double minimum = double.NegativeInfinity)
  {
    // The clear region is bounded by the minimum and the taken slots' neighbours, so the nearest clear
    // slot is the one asked for (raised to the minimum) or one of those neighbours.
    bool IsClear(double candidate) =>
      candidate >= minimum && taken.All(t => Math.Abs(candidate - t) >= 1 - SlotTolerance);

    var wanted = Math.Max(slot, minimum);
    return taken
      .SelectMany(t => new[] { t - 1, t + 1 })
      .Prepend(wanted)
      .Where(IsClear)
      .MinBy(candidate => Math.Abs(candidate - slot));
  }

  private static FamilyTreeLayoutResult Assemble(
    int centerId,
    Families families,
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, string> placedBy,
    FamilyTreeLayoutMetrics metrics)
  {
    var maxGeneration = families.Nodes.Values.Max(node => node.Generation);
    var minSlot = x.Values.Min();
    var origin = Math.Ceiling(metrics.Margin + (metrics.NodeWidth / 2));

    // The one place a slot becomes a pixel, so positions equal in slots stay equal on screen.
    double Snap(double slot) => origin + Math.Round((slot - minSlot) * metrics.SlotPitch, MidpointRounding.AwayFromZero);
    double CentreX(int id) => Snap(x[id]);
    double Top(int generation) => metrics.Margin + ((maxGeneration - generation) * metrics.RowPitch);
    double Bottom(int generation) => Top(generation) + metrics.NodeHeight;
    double Middle(int generation) => Top(generation) + (metrics.NodeHeight / 2);

    var layouts = new List<FamilyTreeNodeLayout>(families.Nodes.Count);
    foreach (var node in families.Nodes.Values)
    {
      var left = CentreX(node.Id) - (metrics.NodeWidth / 2);
      var rect = new Rect(left, Top(node.Generation), metrics.NodeWidth, metrics.NodeHeight);
      layouts.Add(new FamilyTreeNodeLayout(node, rect));
    }

    var drawing = new Drawing();
    var attachments = new Dictionary<int, List<double>>();
    var gapUses = new Dictionary<(int Generation, double Column), int>();
    var hosts = new HashSet<int>();

    // A loop, or a family beyond the first one dropping from the node, leaves it off its centre line,
    // which belongs to the node's own families, and off any column already attached in the same band, as
    // by a loop from a node straight above or below.
    double Attach(int id, bool top, double toward)
    {
      var band = families.Generation(id) + (top ? 1 : 0);
      if (!attachments.TryGetValue(band, out var taken))
        attachments[band] = taken = [];
      var centre = CentreX(id);
      var sign = toward >= centre ? 1 : -1;
      var columns = new[] { 0.25, -0.25, 0.375, -0.375, 0.125, -0.125 }
        .Select(reach => Math.Round(centre + (sign * reach * metrics.NodeWidth)))
        .ToArray();
      var column = columns.FirstOrDefault(c => !taken.Any(t => Math.Abs(t - c) < 1), columns[0]);
      taken.Add(column);
      return column;
    }

    // Where a vertical can cross a row: in a gap between two of its nodes, or past either end.
    double Column(int generation, double from, double to)
    {
      var centres = families.Row(generation).Select(CentreX).Order().ToArray();
      if (centres.Length == 0)
        return from;
      var half = metrics.SlotPitch / 2;
      var candidates = centres
        .Zip(centres.Skip(1), (a, b) => (a + b) / 2)
        .Prepend(centres[0] - half)
        .Append(centres[^1] + half);
      var gap = candidates.MinBy(c => (Math.Abs(c - from) + Math.Abs(c - to), c));
      var uses = gapUses.GetValueOrDefault((generation, gap));
      gapUses[(generation, gap)] = uses + 1;
      // Off the gap's centre line, where a couple's drop may already run.
      var offset = (uses % 2 == 0 ? 1 : -1) * metrics.HorizontalGap / 4;
      return Math.Round(gap + offset);
    }

    // Over the row through the band above it, or below the top row through the band under it.
    void Bridge(int a, int b, FamilyTreeRelation relation, bool isLoop)
    {
      var generation = families.Generation(a);
      var above = generation < maxGeneration;
      var y = above ? Top(generation) : Bottom(generation);
      var band = above ? generation + 1 : generation;
      var bx = CentreX(b);
      var ax = Attach(a, above, bx);
      var ex = Attach(b, above, CentreX(a));
      var run = drawing.Track(band, ax, ex);
      drawing.Line(relation, isLoop, new(ax, y), new(ax, 0, run), new(ex, 0, run), new(ex, y));
    }

    void Loop(int a, int b, FamilyTreeRelation relation)
    {
      if (families.Generation(a) == families.Generation(b))
      {
        Bridge(a, b, relation, isLoop: true);
        return;
      }

      var (upper, lower) = families.Generation(a) > families.Generation(b) ? (a, b) : (b, a);
      var upperGeneration = families.Generation(upper);
      var lowerGeneration = families.Generation(lower);
      var start = Attach(upper, top: false, CentreX(lower));
      var end = Attach(lower, top: true, CentreX(upper));
      var points = new List<Waypoint> { new(start, Bottom(upperGeneration)) };
      var current = start;
      for (var generation = upperGeneration - 1; generation >= lowerGeneration; generation--)
      {
        var next = generation == lowerGeneration ? end : Column(generation, current, end);
        if (Math.Abs(next - current) >= 0.5)
        {
          var run = drawing.Track(generation + 1, current, next);
          points.Add(new(current, 0, run));
          points.Add(new(next, 0, run));
        }
        current = next;
      }
      points.Add(new(end, Top(lowerGeneration)));
      drawing.Line(relation, true, [.. points]);
    }

    foreach (var key in families.Keys)
    {
      var partners = families.Partners(key).OrderBy(p => x[p]).ToArray();
      var children = families.ChildrenOf(key);
      var placed = children.Where(child => placedBy.GetValueOrDefault(child) == key).OrderBy(child => x[child]).ToArray();
      var generation = families.Generation(partners[0]);

      if (placed.Length != 0)
      {
        var together = partners.Length > 1 && families.IsClear(x, partners[0], partners[^1], partners);
        var married = partners.Zip(partners.Skip(1)).All(pair => families.AreSpouses(pair.First, pair.Second));
        (double X, double Top)[] drops;
        if (together && married)
        {
          var drop = (x[partners[0]] + x[partners[^1]]) / 2;
          var dropX = Snap(drop);
          var onPartner = partners.Any(p => Math.Abs(CentreX(p) - dropX) < metrics.NodeWidth / 2);
          drops = [(dropX, onPartner ? Bottom(generation) : Middle(generation))];
        }
        else
        {
          var mean = placed.Average(child => x[child]);
          var host = partners.MinBy(p => (Math.Abs(x[p] - mean), x[p]));
          // Parents with no marriage on record each drop on their own: a line between them would claim one.
          var droppers = together ? partners : [host];
          var toward = Snap(mean);
          var bottom = Bottom(generation);
          // A second family hung from one person, as after a third marriage, drops beside the first.
          drops = [.. droppers.Select(p => (hosts.Add(p) ? CentreX(p) : Attach(p, top: false, toward), bottom))];
          foreach (var partner in partners.Where(p => !droppers.Contains(p) && !families.AreSpouses(p, host)))
            Loop(partner, host, FamilyTreeRelation.ParentChild);
        }

        var childTop = Top(generation - 1);
        var xs = placed.Select(CentreX).ToArray();
        var tops = drops.ToDictionary(d => d.X, d => d.Top);
        var columns = tops.Keys.Union(xs).Order().ToArray();
        bool Through(double column) => tops.ContainsKey(column) && xs.Contains(column);

        foreach (var column in columns.Where(Through))
          drawing.Line(FamilyTreeRelation.ParentChild, false, new(column, tops[column]), new(column, childTop));
        if (columns.Length > 1)
        {
          var (low, high) = (columns[0], columns[^1]);
          var bar = drawing.Track(generation, low, high);
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
          drawing.Line(FamilyTreeRelation.ParentChild, false, first, new(low, 0, bar), new(high, 0, bar), last);
          foreach (var column in columns[1..^1].Where(column => !Through(column)))
          {
            var end = End(column);
            drawing.Line(FamilyTreeRelation.ParentChild, false, new(column, 0, bar), end);
          }
        }
      }

      foreach (var child in children.Except(placed))
      {
        var nearest = partners.MinBy(p => (Math.Abs(x[p] - x[child]), x[p]));
        Loop(nearest, child, FamilyTreeRelation.ParentChild);
      }
    }

    foreach (var (a, b) in families.Spouses)
    {
      if (families.Generation(a) != families.Generation(b))
      {
        Loop(a, b, FamilyTreeRelation.Spouse);
        continue;
      }
      if (!families.IsClear(x, a, b, [a, b]))
      {
        Bridge(a, b, FamilyTreeRelation.Spouse, isLoop: false);
        continue;
      }
      // Centre to centre: the photos cover the ends, so the line reaches each photo however much wider
      // than the photo the node is.
      var (left, right) = x[a] <= x[b] ? (a, b) : (b, a);
      var y = Middle(families.Generation(a));
      drawing.Line(FamilyTreeRelation.Spouse, false, new(CentreX(left), y), new(CentreX(right), y));
    }

    foreach (var (parent, child) in families.OffRowParents)
      Loop(parent, child, FamilyTreeRelation.ParentChild);

    var connectors = drawing.Resolve(Bottom, metrics);
    var points = connectors.SelectMany(connector => connector.Points).ToArray();
    var width = layouts.Max(l => l.Bounds.Right);
    var height = layouts.Max(l => l.Bounds.Bottom);
    if (points.Length != 0)
    {
      width = Math.Max(width, points.Max(p => p.X));
      height = Math.Max(height, points.Max(p => p.Y));
    }
    var centerTopLeft = layouts.Single(l => l.Node.Id == centerId).Bounds.Location;

    return new FamilyTreeLayoutResult(
      layouts,
      connectors,
      new Size(width + metrics.Margin, height + metrics.Margin),
      centerTopLeft,
      metrics);
  }
}
