using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Data;

namespace GT4.Core.Project;

using IFileSystem = Utils.IFileSystem;

internal class ProjectList : IProjectList
{
  private readonly IStorage _Storage;
  private readonly IFileSystem _FileSystem;
  private readonly WeakReference<ProjectInfo[]?> _Items = new(null);

  private async Task<ProjectInfo> GetProjectInfoAsync(IProjectDocument project, CancellationToken token)
  {
    var results = await Task.WhenAll(
        project.Metadata.GetProjectName(token),
        project.Metadata.GetProjectDescription(token));

    return new ProjectInfo(
      Description: results[1] ?? string.Empty,
      Name: results[0] ?? throw new DataException($"There is no name stored in the project"),
      Origin: default!
    );
  }

  private async Task<ProjectInfo> GetProjectInfoAsync(FileDescription origin, CancellationToken token)
  {
    try
    {
      // TODO Implement Project sniffer to upload the stream into memory and retrieve metada from the in-memory DB.

      using var projectHost = await OpenAsync(origin, token);
      using var project = projectHost.Project!;
      var projectInfo = await GetProjectInfoAsync(project, token);

      return projectInfo with { Origin = origin };
    }
    catch (Exception ex)
    {
      //TODO BrokenProjectItem
      return new ProjectInfo(
        Description: ex.ToString(),
        Name: $"Error: {ex.Message}",
        Origin: origin
      );
    }
  }

  private static string GetUniqueProjectName() =>
    $"project-{Guid.NewGuid().ToString("N")}.{ProjectExtension}";

  private static bool CompareNames(string name1, string name2) =>
    string.Equals(name1, name2, StringComparison.InvariantCultureIgnoreCase);

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public ProjectList(IStorage storage, IFileSystem fileSystem)
  {
    _Storage = storage;
    _FileSystem = fileSystem;
  }

  public readonly static string ProjectExtension = "gt4";

  public async Task<ProjectInfo[]> GetItemsAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    try
    {
      var tasks = _FileSystem
        .GetFiles(_Storage.ProjectsRoot, $"*.{ProjectExtension}", true)
        .ToList()
        .Select(path => GetProjectInfoAsync(path, token));
      items = await Task.WhenAll(tasks);

      _Items.SetTarget(items);
      return items;
    }
    catch
    {
      return Array.Empty<ProjectInfo>();
    }
  }

  public async Task<ProjectHost> OpenAsync(FileDescription origin, CancellationToken token)
  {
    var cache = GetCacheFileDescription();
    var host = new ProjectHost(_FileSystem, origin, cache);
    host.Project = await ProjectDocument.OpenAsync(_FileSystem.ToPath(cache), token);

    return host;
  }

  public async Task<ProjectHost> CreateAsync(string projectName, string projectDescription, CancellationToken token)
  {
    var dir = GetProjectDirectoryByName(projectName);
    var origin = new FileDescription(dir, GetUniqueProjectName(), ProjectDocument.MimeType);
    var cache = GetCacheFileDescription();
    using (var file = _FileSystem.OpenWriteStream(origin)) file.Close();
    using var host = new ProjectHost(_FileSystem, origin, cache);
    host.Project = await ProjectDocument.CreateNewAsync(_FileSystem.ToPath(cache), projectName, token);
    await Task.WhenAll(
      host.Project.Metadata.SetProjectName(projectName, token),
      host.Project.Metadata.SetProjectDescription(projectDescription, token));

    InvalidateItems();

    return host;
  }

  public async Task<ProjectInfo> ExportAsync(Stream content, CancellationToken token)
  {
    var temp = GetCacheFileDescription();
    _FileSystem.Copy(content, temp);

    ProjectInfo projectInfo;
    using (var project = await ProjectDocument.OpenAsync(_FileSystem.ToPath(temp), token))
    {
      projectInfo = await GetProjectInfoAsync(project, token);
    }

    var dir = GetProjectDirectoryByName(projectInfo.Name);
    var origin = new FileDescription(dir, GetUniqueProjectName(), ProjectDocument.MimeType);
    _FileSystem.Copy(temp, origin);

    InvalidateItems();

    return projectInfo with { Origin = origin };
  }

  public async Task RemoveAsync(string name, CancellationToken token)
  {
    var modifiableItems = (await GetItemsAsync(token)).ToList();
    var item = modifiableItems.FirstOrDefault(i => CompareNames(i.Name, name));
    if (item?.Name is null)
      return;

    _FileSystem.RemoveFile(item.Origin);

    InvalidateItems();
  }

  private FileDescription GetCacheFileDescription()
  {
    return new FileDescription(_Storage.ApplicationData, Guid.NewGuid().ToString(), ProjectDocument.MimeType);
  }

  public DirectoryDescription GetProjectDirectoryByName(string projectName)
  {
    var directoryName = string.Join(string.Empty,
      projectName
      .Select(c => (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)) ? c.ToString() : string.Format("{0:X2}", (int)c)));

    return _Storage.ProjectsRoot with
    {
      Path = [.._Storage.ProjectsRoot.Path, directoryName]
    };
  }
}
