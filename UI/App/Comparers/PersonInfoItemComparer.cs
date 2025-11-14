using GT4.UI.Items;

namespace GT4.UI.Comparers;

public class PersonInfoItemComparer : IComparer<PersonInfoItem>
{
  public int Compare(PersonInfoItem? x, PersonInfoItem? y)
  {
    return x?.CommonName.CompareTo(y?.CommonName) ?? 0;
  }
}
