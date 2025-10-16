namespace GT4.UI;

public class ProjectListItemCreate : ProjectListItem
{
  public ProjectListItemCreate()
    : base(new Project.ProjectItem
    {
      Description = "Create a new Genealogy Tree",
      Name = "Create New!"
    })
  {
  }
}