namespace GT4.Core.Project.Dto;

public record struct Consanguinity(int Value) : IEquatable<Consanguinity>, IComparable<Consanguinity>
{
  public int CompareTo(Consanguinity other) => Value.CompareTo(other.Value);

  public bool Equals(Consanguinity? other) => Value == other?.Value;

  public override int GetHashCode() => Value.GetHashCode();

  public static Consanguinity operator +(Consanguinity left, Consanguinity right) => 
    new Consanguinity(left.Value + right.Value);

  public static Consanguinity operator -(Consanguinity left, Consanguinity right) => 
    new Consanguinity(left.Value - right.Value);

  public static Consanguinity operator ++(Consanguinity left) => new Consanguinity(left.Value + 1);

  public static Consanguinity operator --(Consanguinity left) => new Consanguinity(left.Value - 1);

  public static bool operator <(Consanguinity left, Consanguinity right) => left.CompareTo(right) < 0;

  public static bool operator >(Consanguinity left, Consanguinity right) => left.CompareTo(right) > 0;

  public static bool operator <=(Consanguinity left, Consanguinity right) => left.CompareTo(right) <= 0;

  public static bool operator >=(Consanguinity left, Consanguinity right) => left.CompareTo(right) >= 0;

  public static readonly Consanguinity Zero = new Consanguinity(0);
}
