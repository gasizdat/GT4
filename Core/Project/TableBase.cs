namespace GT4.Core.Project;

public abstract class TableBase
{
  protected TableBase(ProjectDocument document) => Document = document;

  public ProjectDocument Document { get; init; }

  public abstract Task CreateAsync();
}
