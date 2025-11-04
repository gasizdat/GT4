using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Data;
using System.IO;

namespace GT4.Core.Project;

internal class ProjectList : IProjectList
{
  private readonly IStorage _Storage;
  private readonly IFileSystem _FileSystem;
  private readonly WeakReference<ProjectInfo[]?> _Items = new(null);



  private async Task<ProjectInfo> GetProjectItemAsync(FileDescription origin, CancellationToken token)
  {
    var cacheFile = GetCacheFileDescription();
    try
    {
      using var projectHost = await OpenAsync(origin, token);
      using var project = projectHost.Project!; 

      var results = await Task.WhenAll(
        project.Metadata.GetAsync<string>("name", token),
        project.Metadata.GetAsync<string>("description", token));

      return new ProjectInfo(
        Description: results[1] ?? string.Empty,
        Name: results[0]
          ?? throw new DataException($"There is no name stored in the project ({_FileSystem.ToPath(origin)})"),
        Origin: origin
      );
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
        .Select(path => GetProjectItemAsync(path, token));
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
    var dir = new DirectoryDescription(
      Root: _Storage.ProjectsRoot.Root,
      Path: _Storage.ProjectsRoot.Path.Concat(["local_projects", Guid.NewGuid().ToString()]).ToArray());
    var origin = new FileDescription(dir, $"project.{ProjectExtension}", ProjectDocument.MimeType);
    var cache = GetCacheFileDescription();
    using (var file = _FileSystem.OpenWriteStream(origin)) file.Close();
    var host = new ProjectHost(_FileSystem, origin, cache);
    var project = await ProjectDocument.CreateNewAsync(_FileSystem.ToPath(cache), projectName, token);
    await Task.WhenAll(
      project.Metadata.AddAsync("name", projectName, token),
      project.Metadata.AddAsync("description", projectDescription, token));
    host.Project = project;

    return host;
  }

  public async Task RemoveAsync(string name, CancellationToken token)
  {
    var modifiableItems = (await GetItemsAsync(token)).ToList();
    var item = modifiableItems.FirstOrDefault(i => CompareNames(i.Name, name));
    if (item?.Name is null)
      return;

    InvalidateItems();
  }

  private FileDescription GetCacheFileDescription()
  {
    return new FileDescription(_Storage.ApplicationData, Guid.NewGuid().ToString(), ProjectDocument.MimeType);
  }
}
