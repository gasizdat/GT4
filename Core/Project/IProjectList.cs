namespace GT4.Project;

public interface IProjectList
{
  Task<IReadOnlyList<ProjectItem>> Items { get; }

  Task CreateAsync(ProjectInfo info);
  Task RemoveAsync(string name);
}