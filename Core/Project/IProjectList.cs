
namespace GT4.Project;

public interface IProjectList
{
  IReadOnlyList<ProjectItem> Items { get; }

  void Create(string name);
  void Remove(string name);
}