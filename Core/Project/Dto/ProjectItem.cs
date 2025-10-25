namespace GT4.Core.Project;

public record class ProjectItem : ProjectInfo
{
  public string Path { get; init; } = string.Empty;
}

