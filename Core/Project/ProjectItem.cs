namespace GT4.Project;

public record class ProjectItem : ProjectInfo
{
  public string Path { get; init; } = string.Empty;
}

