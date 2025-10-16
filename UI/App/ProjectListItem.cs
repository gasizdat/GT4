namespace GT4.UI;

public class ProjectListItem
{
  Project.ProjectItem _item;

  public ProjectListItem(Project.ProjectItem item)
  {
    _item = item;
  }

  public string Description => _item.Description;
  public string Name => _item.Name;
  public string Path => _item.Path;
}
