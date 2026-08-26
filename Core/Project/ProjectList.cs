using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Microsoft.Data.Sqlite;
using System.Data;

namespace GT4.Core.Project;

using IFileSystem = Utils.IFileSystem;

internal class ProjectList : IProjectList
{
  private readonly IStorage _Storage;
  private readonly IFileSystem _FileSystem;
  private readonly WeakReference<ProjectInfo[]?> _Items = new(null);
  private readonly static string[] RevisionFileSuffixes = ["", "-journal", "-wal", "-shm"];
  private const int SqliteCantOpen = 14;

  public readonly static string ProjectExtension = "gt4";

  public ProjectList(IStorage storage, IFileSystem fileSystem)
  {
    _Storage = storage;
    _FileSystem = fileSystem;
  }

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
    var host = await OpenAnyVersionAsync(origin, token);
    var document = (ProjectDocument)host.Project!;
    var fileVersion = document.SchemaVersion;
    if (fileVersion == ProjectDocument.CurrentSchemaVersion)
    {
      return host;
    }

    // Disposing an unmodified host drops its cache copy, which would otherwise linger as a revision.
    await host.DisposeAsync();
    throw fileVersion < ProjectDocument.CurrentSchemaVersion
      ? new ProjectSchemaOutdatedException(fileVersion, ProjectDocument.CurrentSchemaVersion)
      : new ProjectSchemaTooNewException(fileVersion, ProjectDocument.CurrentSchemaVersion);
  }

  public async Task UpgradeAsync(FileDescription origin, CancellationToken token)
  {
    await using var host = await OpenAnyVersionAsync(origin, token);
    var document = (ProjectDocument)host.Project!;
    // The migration commits, which stamps a new revision, so disposing the host flushes the upgraded
    // cache back over the origin and keeps the pre-upgrade file as a revision to fall back on.
    await document.UpgradeSchemaAsync(token);
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

  /// <summary>
  /// Drops the cache files killed sessions left behind. Must run with no project open: the working
  /// cache of an open host is indistinguishable from a leftover here.
  /// </summary>
  public async Task SanitizeRevisionsAsync(CancellationToken token)
  {
    var files = _FileSystem.GetFiles(_Storage.ProjectsCache, $"version-*.{ProjectExtension}", true);
    var identified = new List<(FileDescription File, DateTime LastWrite, long Size, long Revision)>();
    foreach (var file in files)
    {
      // Stat before opening: recovering a hot journal rewrites both.
      var lastWrite = _FileSystem.GetLastWriteTime(file);
      var size = _FileSystem.GetFileSize(file);
      var revision = await TryReadRevisionAsync(file, token);
      if (revision is null)
      {
        RemoveRevisionFiles(file);
      }
      else
      {
        identified.Add((file, lastWrite, size, revision.Value));
      }
    }

    // An unmatched counter is a restore point: it holds a commit the origin never received. Size joins
    // the key as a conservatism - an equal-counter pair that differs in length costs a missed cleanup,
    // never a deleted revision.
    var duplicates = identified
      .GroupBy(item => (item.File.Directory, item.Size, item.Revision))
      .SelectMany(group => group.OrderByDescending(item => item.LastWrite).Skip(1));
    foreach (var duplicate in duplicates)
    {
      RemoveRevisionFiles(duplicate.File);
    }
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

  private async Task<ProjectInfo> GetProjectInfoAsync(IProjectDocument project, CancellationToken token)
  {
    // Sequential, not Task.WhenAll: the single-connection gate serializes these onto one connection, so
    // WhenAll buys nothing.
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

  /// <summary>
  /// Opens a project regardless of its schema version, for the two callers that must tolerate a stale
  /// one: the listing, which only reads the name a legacy file still holds, and the upgrade itself.
  /// </summary>
  private async Task<ProjectHost> OpenAnyVersionAsync(FileDescription origin, CancellationToken token)
  {
    var cache = GetCacheFileDescription(origin.FileName);
    var host = new ProjectHost(_FileSystem, origin, cache);
    host.Project = await ProjectDocument.OpenAsync(_FileSystem.ToPath(cache), token);

    return host;
  }

  private async Task<ProjectInfo> GetProjectInfoAsync(FileDescription origin, CancellationToken token)
  {
    try
    {
      using var projectHost = await OpenAnyVersionAsync(origin, token);
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

  // Null means SQLite rejected the file's content - the sweep's definition of garbage. A file it could
  // not open at all propagates instead: something else holds it, and the next sweep retries it.
  private async Task<long?> TryReadRevisionAsync(FileDescription revision, CancellationToken token)
  {
    var path = _FileSystem.ToPath(revision);
    try
    {
      await using var project = await ProjectDocument.OpenReadOnlyAsync(path, token);
      return project.ProjectRevision;
    }
    catch (SqliteException)
    {
      // Read-only cannot roll back a hot journal, and refusing one is indistinguishable here from
      // refusing a corrupt file. The read-write retry recovers the first and only the first.
    }

    try
    {
      await using var project = await ProjectDocument.OpenAsync(path, token);
      return project.ProjectRevision;
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode != SqliteCantOpen)
    {
      return null;
    }
  }

  private void RemoveRevisionFiles(FileDescription revision)
  {
    foreach (var suffix in RevisionFileSuffixes)
    {
      _FileSystem.RemoveFile(revision with { FileName = revision.FileName + suffix });
    }
  }

  private static string GetUniqueProjectName(string prefix) =>
    $"{prefix}-{DateTime.Now.ToLocalTime():yyyy∕MM∕dd-HH﹕mm﹕ss}.{ProjectExtension}";

  private static bool CompareNames(string name1, string name2) =>
    string.Equals(name1, name2, StringComparison.InvariantCultureIgnoreCase);

  private void InvalidateItems() => _Items.SetTarget(null);

  private FileDescription GetCacheFileDescription(string projectName)
  {
    var projectNameWithoutExtension = Path.GetFileNameWithoutExtension(projectName);
    var projectVersionsDir = _Storage.ProjectsCache with
    {
      Path = [.. _Storage.ProjectsCache.Path, projectNameWithoutExtension]
    };
    return new FileDescription(projectVersionsDir, GetUniqueProjectName("version"), IProjectDocument.MimeType);
  }
}
