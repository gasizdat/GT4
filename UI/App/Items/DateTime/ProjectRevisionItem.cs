using GT4.Core.Project.Dto;

namespace GT4.UI.Items;

public class ProjectRevisionItem : CollectionItemBase<ProjectRevision>
{
  public ProjectRevisionItem(ProjectRevision projectRevision)
    : base(projectRevision, string.Empty)
  {
  }

  public string DateTimeText => 
    $"{Info.DateTime.ToLocalTime().ToLongDateString()}, {Info.DateTime.ToLocalTime().ToLongTimeString()}";
}
