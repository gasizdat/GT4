namespace GT4.UI;

using GT4.Core.Project;

public class ProjectListItemCreate : ProjectListItem
{
  public ProjectListItemCreate()
    : base(new ProjectItem
    {
      Description = "Create a new Genealogy Tree",
      Name = "Create New!"
    })
  {
  }
}