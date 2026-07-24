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
    // Sequential, not Task.WhenAll: the reads differ in result type now, and the single-connection gate
    // serializes them onto one connection regardless, so nothing is lost.
    var name = await project.Metadata.GetProjectNameAsync(token);
    var description = await project.Metadata.GetProjectDescriptionAsync(token);
    var revision = await project.Metadata.GetProjectRevisionAsync(token);

    return new ProjectInfo(
      Revision: revision,
      Description: description ?? string.Empty,
      Name: name ?? throw new DataException($"There is no name stored in the project"),
      Origin: default!
    );
  }

  private async Task<ProjectInfo> GetProjectInfoAsync(FileDescription origin, CancellationToken token)
  {
    try
    {
      using var projectHost = await OpenAsync(origin, token);
      using var project = projectHost.Project!;
      var projectInfo = await GetProjectInfoAsync(project, token);

      return projectInfo with { Origin = origin };
    }
    catch (Exception ex)
    {
      return new ProjectInfo(
        Revision: null,
        Description: ex.ToString(),
        Name: $"Error: {ex.Message}",
        Origin: origin
      );
    }
  }

  private static string GetUniqueProjectName(string prefix) =>
    $"{prefix}-{DateTime.Now.ToLocalTime():yyyy∕MM∕dd-HH﹕mm﹕ss}.{ProjectExtension}";

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

    var tasks = _FileSystem
      .GetFiles(_Storage.ProjectsRoot, $"*.{ProjectExtension}", true)
      .ToList()
      .Select(path => GetProjectInfoAsync(path, token));
    items = await Task.WhenAll(tasks);

    _Items.SetTarget(items);
    return items;
  }

  public async Task<ProjectHost> OpenAsync(FileDescription origin, CancellationToken token)
  {
    var cache = GetCacheFileDescription(origin.FileName);
    var host = new ProjectHost(_FileSystem, origin, cache);
    host.Project = await ProjectDocument.OpenAsync(_FileSystem.ToPath(cache), token);

    return host;
  }

  public async Task<ProjectHost> CreateAsync(string projectName, string projectDescription, CancellationToken token)
  {
    var dir = GetProjectDirectoryByName(projectName);
    var prefix = dir.Path.Last();
    var projectFileName = GetUniqueProjectName(prefix);
    var origin = new FileDescription(dir, projectFileName, IProjectDocument.MimeType);
    var cache = GetCacheFileDescription(projectFileName);
    using (var file = _FileSystem.OpenWriteStream(origin)) file.Close();
    // The host is returned live and owned by the caller: disposing it flushes the freshly written
    // cache back to the origin.
    var host = new ProjectHost(_FileSystem, origin, cache);
    try
    {
      host.Project = await ProjectDocument.CreateNewAsync(_FileSystem.ToPath(cache), projectName, token);
      await Task.WhenAll(
        host.Project.Metadata.SetProjectNameAsync(projectName, token),
        host.Project.Metadata.SetProjectDescriptionAsync(projectDescription, token));
    }
    catch
    {
      await host.DisposeAsync();
      throw;
    }

    InvalidateItems();

    return host;
  }

  public async Task<ProjectInfo> ImportAsync(Stream content, CancellationToken token)
  {
    var importedFileName = GetUniqueProjectName("imported");
    var temp = GetCacheFileDescription(importedFileName);

    try
    {
      ProjectInfo projectInfo;
      _FileSystem.Copy(content, temp);
      using (var project = await ProjectDocument.OpenAsync(_FileSystem.ToPath(temp), token))
      {
        projectInfo = await GetProjectInfoAsync(project, token);
      }

      var dir = GetProjectDirectoryByName(projectInfo.Name);
      var prefix = dir.Path.Last();
      var projectFileName = GetUniqueProjectName(prefix);
      var origin = new FileDescription(dir, projectFileName, IProjectDocument.MimeType);
      _FileSystem.Copy(temp, origin);

      InvalidateItems();
      return projectInfo with { Origin = origin };
    }
    finally
    {
      _FileSystem.RemoveFile(temp);
    }
  }

  public Task RemoveAsync(FileDescription origin, CancellationToken token)
  {
    _FileSystem.RemoveFile(origin);
    InvalidateItems();
    return Task.CompletedTask;
  }

  private FileDescription GetCacheFileDescription(string projectName)
  {
    var projectNameWithoutExtension = Path.GetFileNameWithoutExtension(projectName);
    var projectVersionsDir = _Storage.ProjectsCache with
    {
      Path = [.. _Storage.ProjectsCache.Path, projectNameWithoutExtension]
    };
    return new FileDescription(projectVersionsDir, GetUniqueProjectName("version"), IProjectDocument.MimeType);
  }

  public DirectoryDescription GetProjectDirectoryByName(string projectName)
  {
    var directoryName = string.Join(string.Empty,
      projectName
      .Select(c => (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)) ? c.ToString() : string.Format("{0:X2}", (int)c)));

    return _Storage.ProjectsRoot with
    {
      Path = [.. _Storage.ProjectsRoot.Path, directoryName]
    };
  }
}
