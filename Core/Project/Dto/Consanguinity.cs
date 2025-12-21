namespace GT4.Core.Project.Dto;

public record struct Consanguinity(int Value) : IEquatable<Consanguinity>, IComparable<Consanguinity>
{
  public int CompareTo(Consanguinity other)
  {
    return Value.CompareTo(other.Value);
  }

  public bool Equals(Consanguinity? other) => Value == other?.Value;

  public override int GetHashCode()
  {
    return Value.GetHashCode();
  }

  public static Consanguinity operator +(Consanguinity left, Consanguinity right)
  {
    return new Consanguinity(left.Value + right.Value);
  }

  public static Consanguinity operator -(Consanguinity left, Consanguinity right)
  {
    return new Consanguinity(left.Value + right.Value);
  }

  public static Consanguinity operator ++(Consanguinity left)
  {
    return new Consanguinity(left.Value + 1);
  }

  public static Consanguinity operator --(Consanguinity left)
  {
    return new Consanguinity(left.Value - 1);
  }

  public static readonly Consanguinity Zero = new Consanguinity(0);
}
