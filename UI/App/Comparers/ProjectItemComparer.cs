using GT4.UI.App.Items;

namespace GT4.UI.Comparers;

public class ProjectItemComparer : IComparer<ProjectItem>
{
  public int Compare(ProjectItem? x, ProjectItem? y)
  {
    return x?.Name.CompareTo(y?.Name) ?? 0;
  }
}
