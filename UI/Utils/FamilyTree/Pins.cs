namespace GT4.UI.Utils.Genealogy;

internal static class Pins
{
  public static Dictionary<int, double> Resolve(
    int centerId,
    Families families,
    IReadOnlyDictionary<int, double> x,
    IReadOnlyDictionary<int, double>? pins)
  {
    var pinned = new Dictionary<int, double>();
    var applicable = pins?.Where(pin => x.ContainsKey(pin.Key)).ToArray() ?? [];
    if (applicable.Length == 0)
      return pinned;

    var centerX = x[centerId];
    pinned[centerId] = centerX;
    foreach (var (id, offset) in applicable)
    {
      var generation = families.Generation(id);
      var taken = pinned
        .Where(pin => families.Generation(pin.Key) == generation)
        .Select(pin => pin.Value)
        .ToArray();
      pinned[id] = FamilyTreeLayout.NearestClearSlot(centerX + offset, taken);
    }
    return pinned;
  }

  // Moves the free nodes of each pinned row as little as keeps them in order and a slot clear of each
  // other and of the pins. A gap between two pins takes only as many nodes as fit; the rest move on.
  public static void MakeRoom(Families families, Dictionary<int, double> x, IReadOnlyDictionary<int, double> pinned)
  {
    foreach (var generation in families.Nodes.Values.Select(node => node.Generation).Distinct())
    {
      var row = families.Row(generation);
      var pins = row.Where(pinned.ContainsKey).Select(id => x[id]).Order().ToArray();
      if (pins.Length == 0)
        continue;

      var free = row.Where(id => !pinned.ContainsKey(id)).OrderBy(id => x[id]).ThenBy(id => id).ToArray();
      var segments = Enumerable.Range(0, pins.Length + 1).Select(_ => new List<int>()).ToArray();
      int Capacity(int segment) => segment == 0 || segment == pins.Length
        ? int.MaxValue
        : (int)Math.Floor(pins[segment] - pins[segment - 1] + FamilyTreeLayout.SlotTolerance) - 1;

      var current = 0;
      foreach (var id in free)
      {
        var segment = Math.Max(current, pins.Count(pin => pin < x[id]));
        while (segments[segment].Count >= Capacity(segment))
          segment++;
        segments[segment].Add(id);
        current = segment;
      }

      for (var segment = 0; segment < segments.Length; segment++)
      {
        var members = segments[segment];
        if (members.Count == 0)
          continue;
        var low = segment == 0 ? double.NegativeInfinity : pins[segment - 1] + 1;
        var high = segment == pins.Length ? double.PositiveInfinity : pins[segment] - members.Count;
        // With z = x - index the order-and-gap constraint is just z non-decreasing, so the nearest such
        // row is the isotonic regression of the wanted positions, clamped to the room between the pins.
        var wanted = members.Select((id, index) => x[id] - index).ToArray();
        var z = Isotonic(wanted);
        for (var index = 0; index < members.Count; index++)
          x[members[index]] = Math.Clamp(z[index], low, high) + index;
      }
    }
  }

  // Pool-adjacent-violators: the non-decreasing sequence nearest to the values in least squares.
  private static double[] Isotonic(double[] values)
  {
    var means = new List<double>();
    var counts = new List<int>();
    foreach (var value in values)
    {
      means.Add(value);
      counts.Add(1);
      while (means.Count > 1 && means[^2] > means[^1])
      {
        var count = counts[^2] + counts[^1];
        var mean = ((means[^2] * counts[^2]) + (means[^1] * counts[^1])) / count;
        means.RemoveAt(means.Count - 1);
        counts.RemoveAt(counts.Count - 1);
        means[^1] = mean;
        counts[^1] = count;
      }
    }
    return [.. means.SelectMany((mean, block) => Enumerable.Repeat(mean, counts[block]))];
  }
}
