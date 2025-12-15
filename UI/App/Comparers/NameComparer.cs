using GT4.Core.Project.Dto;

namespace GT4.UI.Comparers;

public class NameComparer : IComparer<Name>
{
  public int Compare(Name? x, Name? y)
  {
    return x?.Value.CompareTo(y?.Value) ?? 0;
  }
}
