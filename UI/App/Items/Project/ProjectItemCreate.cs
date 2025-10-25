namespace GT4.UI;

using GT4.Core.Project;

public record class ProjectItemCreate : ProjectItem
{
  public ProjectItemCreate()
    : base(new ProjectItem
    {
      Description = "Create a new Genealogy Tree",
      Name = "Create New!"
    })
  {
  }
}