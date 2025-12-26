namespace GT4.Core.Project.Dto;

public record struct Generation(int Value) : IEquatable<Generation>, IComparable<Generation>
{
  public Generation(RelationshipType relationshipType)
    : this(relationshipType switch
    {
      RelationshipType.Parent => Parent.Value,
      RelationshipType.AdoptiveParent => Parent.Value,
      RelationshipType.Child => Child.Value,
      RelationshipType.AdoptiveChild => Child.Value,
      RelationshipType.Spouse => Zero.Value,
      _ => throw new ApplicationException($"Unexpected type of relationship {relationshipType}")
    })
  { }

  public int CompareTo(Generation other) => Value.CompareTo(other.Value);

  public bool Equals(Generation? other) => Value == other?.Value;

  public override int GetHashCode() => Value.GetHashCode();

  public static Generation operator +(Generation left, Generation right) => new Generation(left.Value + right.Value);

  public static Generation operator -(Generation left, Generation right) => new Generation(left.Value - right.Value);

  public static Generation operator ++(Generation left) => new Generation(left.Value + 1);

  public static Generation operator --(Generation left) => new Generation(left.Value - 1);

  public static bool operator <(Generation left, Generation right) => left.CompareTo(right) < 0;

  public static bool operator >(Generation left, Generation right) => left.CompareTo(right) > 0;

  public static bool operator <=(Generation left, Generation right) => left.CompareTo(right) <= 0;

  public static bool operator >=(Generation left, Generation right) => left.CompareTo(right) >= 0;

  public static readonly Generation Parent = new Generation(1);
  public static readonly Generation Child = new Generation(-1);
  public static readonly Generation Zero = new Generation(0);
}
