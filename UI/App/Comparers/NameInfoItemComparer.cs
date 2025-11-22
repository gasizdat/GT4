using GT4.UI.Items;

namespace GT4.UI.Comparers;

public class NameInfoItemComparer : IComparer<NameInfoItem>
{
  public int Compare(NameInfoItem? x, NameInfoItem? y)
  {
    return x?.Value.CompareTo(y?.Value) ?? 0;
  }
}
