using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Genealogy;

internal sealed class Drawing
{
  private readonly List<(FamilyTreeRelation Relation, bool IsLoop, Waypoint[] Points)> _Lines = [];
  private readonly List<Run> _Runs = [];

  public Run Track(int band, double from, double to)
  {
    var run = new Run(band, from, to);
    _Runs.Add(run);
    return run;
  }

  public void Line(FamilyTreeRelation relation, bool isLoop, params Waypoint[] points) =>
    _Lines.Add((relation, isLoop, points));

  // Left-edge channel routing: runs that overlap, or come within half a gap of each other, in one band
  // take separate tracks spread evenly through the band.
  public FamilyTreeConnector[] Resolve(Func<int, double> bandTop, FamilyTreeLayoutMetrics metrics)
  {
    foreach (var band in _Runs.GroupBy(run => run.Band))
    {
      var tracks = new List<double>();
      var assigned = new List<(Run Run, int Track)>();
      foreach (var run in band.OrderBy(run => run.Left).ThenBy(run => run.Right))
      {
        var track = tracks.FindIndex(right => run.Left >= right + (metrics.HorizontalGap / 2));
        if (track < 0)
        {
          track = tracks.Count;
          tracks.Add(run.Right);
        }
        tracks[track] = run.Right;
        assigned.Add((run, track));
      }
      var top = bandTop(band.Key);
      foreach (var (run, track) in assigned)
        run.Y = Math.Round(top + ((track + 1) * metrics.VerticalGap / (tracks.Count + 1)));
    }

    return [.. _Lines.Select(line => new FamilyTreeConnector(line.Relation, Simplify(line.Points), line.IsLoop))];
  }

  // Drops repeated points and the middle of three in a line, so every point left is a real bend.
  private static PointF[] Simplify(Waypoint[] waypoints)
  {
    var points = new List<PointF>();
    foreach (var waypoint in waypoints)
    {
      var point = new PointF((float)waypoint.X, (float)(waypoint.Track?.Y ?? waypoint.Y));
      if (points.Count != 0 && points[^1] == point)
        continue;
      if (points.Count >= 2)
      {
        var (a, b) = (points[^2], points[^1]);
        var straight = (a.X == b.X && b.X == point.X) || (a.Y == b.Y && b.Y == point.Y);
        if (straight)
          points.RemoveAt(points.Count - 1);
      }
      points.Add(point);
    }
    return [.. points];
  }
}
