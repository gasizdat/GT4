using GT4.UI.App.Items;

namespace GT4.UI.Comparers;

public class FamilyInfoItemComparer : IComparer<FamilyInfoItem>
{
  public int Compare(FamilyInfoItem? x, FamilyInfoItem? y)
  {
    return x?.Info.Value.CompareTo(y?.Info.Value) ?? 0;
  }
}
