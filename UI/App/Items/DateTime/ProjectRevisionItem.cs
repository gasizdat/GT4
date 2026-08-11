using GT4.Core.Project.Dto;
using GT4.UI.Utils;

namespace GT4.UI.Items;

public class ProjectRevisionItem : CollectionItemBase<ProjectRevision>
{
  public ProjectRevisionItem(ProjectRevision projectRevision, ImageUtils imageUtils)
    : base(projectRevision, string.Empty, imageUtils)
  {
  }

  public string DateTimeText =>
    $"{Info.DateTime.ToLocalTime().ToLongDateString()}, {Info.DateTime.ToLocalTime().ToLongTimeString()}";
}
