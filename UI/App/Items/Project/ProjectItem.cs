using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class ProjectItem : CollectionItemBase<ProjectInfo>
{
  public ProjectItem(ProjectInfo info)
    : base (info, "project_icon.png")
  {
  }

  public string Description => Info.Description;
  public string Name => Info.Name;
}
