namespace GT4.Project;

public interface IProjectList
{
  IReadOnlyList<ProjectItem> Items { get; }

  Task CreateAsync(string name);
  Task RemoveAsync(string name);
}