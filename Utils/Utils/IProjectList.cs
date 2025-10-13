
namespace GT4.Utils;

public interface IProjectList : IDisposable
{
  IReadOnlyList<ProjectItem> Items { get; }

  void Add(string name, string path);
  void Remove(string name);
}