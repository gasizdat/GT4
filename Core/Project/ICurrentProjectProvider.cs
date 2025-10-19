
namespace GT4.Core.Project;

public interface ICurrentProjectProvider
{
  ProjectItem Item { get; }
  ProjectDocument Project { get; }
  bool HasCurrentProject { get; }

  Task CloseAsync(CancellationToken token);
  Task OpenAsync(ProjectItem item, CancellationToken token);
}