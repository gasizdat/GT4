using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.Core.Project;

public interface IProjectList
{
  DirectoryDescription GetProjectDirectoryByName(string projectName);
  Task<ProjectInfo[]> GetItemsAsync(CancellationToken token);
  Task<ProjectHost> CreateAsync(string projectName, string projectDescription, CancellationToken token);
  Task<ProjectHost> OpenAsync(FileDescription origin, CancellationToken token);
  Task<ProjectInfo> ExportAsync(Stream content, CancellationToken token);
  Task RemoveAsync(string name, CancellationToken token);
}