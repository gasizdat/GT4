namespace GT4.UI;

using GT4.Core.Project;

public class ProjectListItem
{
  ProjectItem _item;

  public ProjectListItem(ProjectItem item)
  {
    _item = item;
  }

  public string Description => _item.Description;
  public string Name => _item.Name;
  public string Path => _item.Path;
}
