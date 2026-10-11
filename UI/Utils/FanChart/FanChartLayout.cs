using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

/// <summary>
/// Lays a <see cref="FamilyTree"/>'s ancestors out as a fan: the centre person in a disc, each generation
/// one ring further out, symmetric about straight up with the father's side on the left.
/// <para>
/// Slots follow Ahnentafel numbering, so pedigree collapse owes one person several of them. They are
/// filled shallowest first, father's side first: a person is drawn in the first slot they reach and only
/// marked in any later one, behind which their ancestors are left out.
/// </para>
/// </summary>
public static class FanChartLayout
{
  private const int CenterSlot = 1;

  private readonly record struct Placement(int Slot, int Generation, FamilyTreeNode Node, bool IsRepeat);

  private readonly record struct LabelFit(FanChartLabel Label, double LineHeight, Size Size, double Rotation);

  public static FanChartLayoutResult Compute(FamilyTree tree, int generations, double sweepDegrees, FanChartMetrics metrics)
  {
    ArgumentNullException.ThrowIfNull(tree);
    ArgumentNullException.ThrowIfNull(metrics);

    var placements = Place(tree, generations);
    var outerGeneration = placements.Count == 0 ? 0 : placements.Max(placement => placement.Generation);
    var (origin, canvasSize) = Envelope(outerGeneration, sweepDegrees, metrics);
    var segments = placements.Select(placement => Segment(placement, sweepDegrees, origin, metrics)).ToArray();
    var center = tree.Nodes.Single(node => node.Id == tree.CenterId);

    return new FanChartLayoutResult(center, segments, outerGeneration, canvasSize, origin, metrics);
  }

  /// <returns>The person drawn under <paramref name="point"/>, the centre included.</returns>
  public static FamilyTreeNode? HitTest(FanChartLayoutResult layout, Point point)
  {
    var metrics = layout.Metrics;
    var dx = point.X - layout.Origin.X;
    var dy = point.Y - layout.Origin.Y;
    var radius = Math.Sqrt((dx * dx) + (dy * dy));
    if (radius <= metrics.CenterRadius)
      return layout.Center;

    var bearing = Math.Atan2(dx, -dy) * 180 / Math.PI;
    var segment = layout.Segments.SingleOrDefault(segment =>
      radius >= segment.InnerRadius && radius < segment.OuterRadius &&
      bearing >= segment.StartAngle && bearing < segment.StartAngle + segment.SweepAngle);

    return segment?.Node;
  }

  public static Point PointAt(Point origin, double radius, double bearing)
  {
    var radians = bearing * Math.PI / 180;
    return new Point(origin.X + (radius * Math.Sin(radians)), origin.Y - (radius * Math.Cos(radians)));
  }

  public static Rect SectorBounds(Point origin, double innerRadius, double outerRadius, double startAngle, double sweepAngle)
  {
    var endAngle = startAngle + sweepAngle;
    double[] axes = [-180, -90, 0, 90, 180];
    // An arc reaches furthest at its two ends and wherever it crosses an axis.
    var bearings = axes
      .Where(bearing => bearing > startAngle && bearing < endAngle)
      .Append(startAngle)
      .Append(endAngle)
      .ToArray();
    var innerPoints = bearings.Select(bearing => PointAt(origin, innerRadius, bearing));
    var outerPoints = bearings.Select(bearing => PointAt(origin, outerRadius, bearing));
    var points = innerPoints.Concat(outerPoints).ToArray();
    var left = points.Min(point => point.X);
    var top = points.Min(point => point.Y);
    var right = points.Max(point => point.X);
    var bottom = points.Max(point => point.Y);

    return new Rect(left, top, right - left, bottom - top);
  }

  private static List<Placement> Place(FamilyTree tree, int generations)
  {
    var nodes = tree.Nodes.ToDictionary(node => node.Id);
    var parentIds = tree
      .Edges
      .Where(edge => edge.Relation == FamilyTreeRelation.ParentChild)
      .ToLookup(edge => edge.ToId, edge => edge.FromId);
    var drawnIds = new HashSet<int> { tree.CenterId };
    var placements = new List<Placement>();
    List<Placement> frontier = [new(CenterSlot, 0, nodes[tree.CenterId], false)];

    for (var generation = 1; generation <= generations && frontier.Count != 0; generation++)
    {
      var next = new List<Placement>();
      foreach (var child in frontier)
      {
        var parents = parentIds[child.Node.Id].Select(id => nodes[id]);
        var (father, mother) = AssignParents(parents);
        (int Slot, FamilyTreeNode? Parent)[] slots = [(2 * child.Slot, father), ((2 * child.Slot) + 1, mother)];

        foreach (var (slot, parent) in slots)
        {
          if (parent is null)
            continue;

          var placement = new Placement(slot, generation, parent, IsRepeat: !drawnIds.Add(parent.Id));
          placements.Add(placement);
          if (!placement.IsRepeat)
            next.Add(placement);
        }
      }

      frontier = next;
    }

    return placements;
  }

  // By sex first, so a lone mother stays on the mother's side; anyone else takes a free slot by id.
  private static (FamilyTreeNode? Father, FamilyTreeNode? Mother) AssignParents(IEnumerable<FamilyTreeNode> parents)
  {
    var ordered = parents.OrderBy(node => node.Id).ToArray();
    var father = ordered.FirstOrDefault(node => node.Person.BiologicalSex == BiologicalSex.Male);
    var mother = ordered.FirstOrDefault(node => node.Person.BiologicalSex == BiologicalSex.Female);
    var spare = ordered.Where(node => node != father && node != mother).ToArray();
    father ??= spare.FirstOrDefault();
    mother ??= spare.FirstOrDefault(node => node != father);

    return (father, mother);
  }

  private static (Point Origin, Size CanvasSize) Envelope(int outerGeneration, double sweepDegrees, FanChartMetrics metrics)
  {
    var center = new Point(0, 0);
    var radius = metrics.CenterRadius + (outerGeneration * metrics.RingWidth);
    var disc = SectorBounds(center, 0, metrics.CenterRadius, -180, 360);
    var fan = SectorBounds(center, 0, radius, -sweepDegrees / 2, sweepDegrees);
    var bounds = disc.Union(fan);
    var origin = new Point(metrics.Margin - bounds.Left, metrics.Margin - bounds.Top);
    var size = new Size(bounds.Width + (2 * metrics.Margin), bounds.Height + (2 * metrics.Margin));

    return (origin, size);
  }

  private static FanChartSegment Segment(Placement placement, double sweepDegrees, Point origin, FanChartMetrics metrics)
  {
    var count = 1 << placement.Generation;
    var sweep = sweepDegrees / count;
    var start = (-sweepDegrees / 2) + ((placement.Slot - count) * sweep);
    var inner = metrics.CenterRadius + ((placement.Generation - 1) * metrics.RingWidth);
    var outer = inner + metrics.RingWidth;
    var bearing = start + (sweep / 2);
    var fit = FitLabel(inner, outer, sweep, bearing, metrics);
    var labelCenter = PointAt(origin, (inner + outer) / 2, bearing);

    return new FanChartSegment(
      placement.Slot,
      placement.Generation,
      placement.Node,
      placement.IsRepeat,
      start,
      sweep,
      inner,
      outer,
      labelCenter,
      fit.Rotation,
      fit.Size,
      fit.LineHeight,
      fit.Label);
  }

  private static LabelFit FitLabel(double inner, double outer, double sweep, double bearing, FanChartMetrics metrics)
  {
    var middle = (inner + outer) / 2;
    var arcLength = middle * sweep * Math.PI / 180;
    var isAlongRing = arcLength >= metrics.RingWidth;
    var halfSweepTan = Math.Tan(sweep * Math.PI / 360);
    // Across a radial label the segment is narrowest at its inner end.
    var thickness = isAlongRing ? metrics.RingWidth : 2 * inner * halfSweepTan;
    // Only a name shrinks: shrunk years would leave an outer ring's names larger than an inner ring's.
    // The floor never rises above the zoomed size, so zooming out still drops the labels that no longer fit.
    var minLineHeight = Math.Min(metrics.MinLineHeight, metrics.LineHeight);
    var label = thickness >= 2 * metrics.LineHeight
      ? FanChartLabel.Full
      : thickness >= minLineHeight ? FanChartLabel.NameOnly : FanChartLabel.None;
    var lineHeight = Math.Min(metrics.LineHeight, thickness);

    if (isAlongRing)
    {
      // A label is straight, so its two lines of text must stop short of the segment's sides, which the
      // lower line meets first, and of the ring's outer edge, which the upper line meets first.
      var betweenSides = 2 * (middle - lineHeight) * halfSweepTan;
      var textTop = middle + lineHeight;
      var withinRing = 2 * Math.Sqrt((outer * outer) - (textTop * textTop));
      var alongRing = Math.Min(betweenSides, withinRing);
      return new LabelFit(label, lineHeight, new Size(alongRing, thickness), Upright(bearing));
    }

    // Likewise a radial label's outer corners must stop short of the outer edge.
    var halfThickness = thickness / 2;
    var radial = 2 * (Math.Sqrt((outer * outer) - (halfThickness * halfThickness)) - middle);
    return new LabelFit(label, lineHeight, new Size(radial, thickness), Upright(bearing - 90));
  }

  // A line of text turned half a turn reads the same line, upside down.
  private static double Upright(double degrees)
  {
    var upright = degrees;
    while (upright > 90)
      upright -= 180;
    while (upright <= -90)
      upright += 180;

    return upright;
  }
}
