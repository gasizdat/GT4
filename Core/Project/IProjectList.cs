namespace GT4.Project;

public interface IProjectList
{
  Task<IReadOnlyList<ProjectItem>> Items { get; }

  Task CreateAsync(string name, string description);
  Task RemoveAsync(string name);
}