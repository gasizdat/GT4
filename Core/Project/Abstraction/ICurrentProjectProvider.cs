using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface ICurrentProjectProvider
{
  ProjectInfo Info { get; }
  IProjectDocument Project { get; }
  bool HasCurrentProject { get; }

  Task UpdateOriginAsync(CancellationToken token);
  Task CloseAsync(CancellationToken token);
  Task OpenAsync(ProjectInfo info, CancellationToken token);
}