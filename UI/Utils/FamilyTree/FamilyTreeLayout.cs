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
      Pins.MakeRoom(families, x, pinned);
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
    var grid = new PixelGrid(families, x, metrics);
    var layouts = new List<FamilyTreeNodeLayout>(families.Nodes.Count);
    foreach (var node in families.Nodes.Values)
    {
      var left = grid.CenterX(node.Id) - (metrics.NodeWidth / 2);
      var rect = new Rect(left, grid.Top(node.Generation), metrics.NodeWidth, metrics.NodeHeight);
      layouts.Add(new FamilyTreeNodeLayout(node, rect));
    }

    var router = new ConnectorRouter(families, x, placedBy, grid, metrics);
    var connectors = router.Route();
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
