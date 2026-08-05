using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Comparers;

public class NameComparer : IComparer<Name>
{
  public int Compare(Name? x, Name? y) => x?.Value.CompareTo(y?.Value) ?? 0;
}
