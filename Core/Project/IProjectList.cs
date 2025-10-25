using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public interface IProjectList
{
  Task<ProjectInfo[]> GetItemsAsync(CancellationToken token);
  Task CreateAsync(ProjectInfo info, CancellationToken token);
  Task RemoveAsync(string name, CancellationToken token);
}