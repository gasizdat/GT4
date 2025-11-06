namespace GT4.Core.Utils;

public record class DirectoryDescription(Environment.SpecialFolder Root, string[] Path)
{
  public override int GetHashCode()
  {
    var ret = Root.GetHashCode();
    foreach (var item in Path)
    {
      ret = HashCode.Combine(ret, item.GetHashCode());
    }
    return ret;
  }


  public virtual bool Equals(DirectoryDescription? other)
  {
    return Root == other?.Root && Path.SequenceEqual(other.Path, StringComparer.Ordinal);
  }

}