using GT4.UI.App.Items;

namespace GT4.UI.Comparers;

public class FamilyMemberInfoItemComparer : IComparer<FamilyMemberInfoItem>
{
  public int Compare(FamilyMemberInfoItem? x, FamilyMemberInfoItem? y)
  {
    return x?.CommonName.CompareTo(y?.CommonName) ?? 0;
  }
}