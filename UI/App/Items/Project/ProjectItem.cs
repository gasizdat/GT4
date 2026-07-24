using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Items;

public class ProjectItem : CollectionItemBase<ProjectInfo>
{
  public ProjectItem(ProjectInfo info)
    : base (info, "project_icon.png")
  {
  }

  public string Description => Info.Description;

  public string Name => Info.Name;

  // The counter starts at 1, so null or the initial value means "no revision".
  public string Revision => Info.Revision is null or ProjectInfo.InitialRevision
    ? string.Empty
    : string.Format(UIStrings.FieldRevision_1, Info.Revision);

  public bool DescriptionVisible => !string.IsNullOrWhiteSpace(Description);

  public bool RevisionVisible => !string.IsNullOrWhiteSpace(Revision);
}
