using GT4.Core.Project.Dto;

namespace GT4.UI.Utils.Comparers;

public class ProjectInfoComparer : IComparer<ProjectInfo>
{
  public int Compare(ProjectInfo? x, ProjectInfo? y)
  {
    return x?.Name.CompareTo(y?.Name) ?? 0;
  }
}
