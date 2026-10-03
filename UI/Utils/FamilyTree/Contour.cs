namespace GT4.UI.Utils.Genealogy;

// A placed subtree: the x of every person in it and its leftmost and rightmost x on each row.
internal sealed class Contour
{
  private readonly Dictionary<int, (double Left, double Right)> _Rows = [];

  public Dictionary<int, double> X { get; } = [];

  public void Place(int id, int generation, double x)
  {
    X[id] = x;
    _Rows[generation] = _Rows.TryGetValue(generation, out var row)
      ? (Math.Min(row.Left, x), Math.Max(row.Right, x))
      : (x, x);
  }

  public void Absorb(Contour other, double dx)
  {
    foreach (var (id, x) in other.X)
      X[id] = x + dx;
    foreach (var (generation, (left, right)) in other._Rows)
    {
      _Rows[generation] = _Rows.TryGetValue(generation, out var row)
        ? (Math.Min(row.Left, left + dx), Math.Max(row.Right, right + dx))
        : (left + dx, right + dx);
    }
  }

  // How far right other must move to stay a slot clear of this contour on every row they share; -∞ when
  // they share none.
  public double ClearanceTo(Contour other)
  {
    var clearance = double.NegativeInfinity;
    foreach (var (generation, row) in _Rows)
    {
      if (other._Rows.TryGetValue(generation, out var next))
        clearance = Math.Max(clearance, row.Right + 1 - next.Left);
    }
    return clearance;
  }

  // Beside it on the right, even when the two share no row.
  public double OffsetBeside(Contour other)
  {
    var clearance = ClearanceTo(other);
    if (!double.IsNegativeInfinity(clearance))
      return clearance;
    var right = _Rows.Values.Max(row => row.Right);
    var left = other._Rows.Values.Min(row => row.Left);
    return right + 1 - left;
  }

  public void Append(Contour other)
  {
    var dx = X.Count == 0 ? 0 : ClearanceTo(other);
    Absorb(other, dx);
  }
}
