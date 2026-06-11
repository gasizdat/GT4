using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.Core.Project;

using IFileSystem = Utils.IFileSystem;

public class ProjectHost : IAsyncDisposable, IDisposable
{
  private readonly IFileSystem _FileSystem;
  private readonly FileDescription _Origin;
  private readonly FileDescription _Cache;
  private readonly object _Sync = new();
  private long? _ProjectRevision;
  private IProjectDocument? _Project = null;

  public ProjectHost(IFileSystem fileSystem, FileDescription origin, FileDescription cache)
  {
    _FileSystem = fileSystem;
    _Origin = origin;
    _Cache = cache;
    _FileSystem.Copy(_Origin, _Cache);
  }

  public IProjectDocument? Project
  {
    get
    {
      lock (_Sync)
      {
        return _Project;
      }
    }
    set
    {
      lock (_Sync)
      {
        _Project = value;
        _ProjectRevision = _Project?.ProjectRevision;
      }
    }
  }

  public void Dispose()
  {
    IProjectDocument project;
    long? openedRevision;
    lock (_Sync)
    {
      if (_Project is null)
      {
        return;
      }
      project = _Project;
      openedRevision = _ProjectRevision;
      _Project = null;
    }

    var actualRevision = project.ProjectRevision;
    project.Dispose();
    if (actualRevision != openedRevision)
    {
      _FileSystem.Copy(_Cache, _Origin);
    }
    else
    {
      _FileSystem.RemoveFile(_Cache);
    }
  }

  public async ValueTask DisposeAsync()
  {
    IProjectDocument project;
    long? openedRevision;
    lock (_Sync)
    {
      if (_Project is null)
      {
        return;
      }
      project = _Project;
      openedRevision = _ProjectRevision;
      _Project = null;
    }

    var actualRevision = project.ProjectRevision;
    await project.DisposeAsync();

    for (var i = 0; i < 5; i++)
    {
      try
      {
        if (actualRevision != openedRevision)
        {
          _FileSystem.Copy(_Cache, _Origin);
          actualRevision = openedRevision ?? 0;
        }
        else
        {
          _FileSystem.RemoveFile(_Cache);
        }
        break;
      }
      catch
      {
        var backoffInterval = TimeSpan.FromMilliseconds(100 + 200 * i);
        await Task.Delay(backoffInterval);
      }
    }
  }

  public ICollection<ProjectRevision> Revisions => _FileSystem
    .GetFiles(_Cache.Directory, $"version-*.{ProjectList.ProjectExtension}", false)
    .Where(f => f != _Cache)
    .Select(f => new ProjectRevision(DateTime: _FileSystem.GetLastWriteTime(f), FileDescription: f))
    .OrderByDescending(r => r.DateTime)
    .ToList();

  public async Task RestoreRevisionAsync(ProjectRevision projectRevision, CancellationToken cancellationToken)
  {
    IProjectDocument? project;
    lock (_Sync)
    {
      project = _Project;
      _Project = null;
    }

    if (project is null)
    {
      throw new ApplicationException("No project is set");
    }
    if (_FileSystem.GetLastWriteTime(projectRevision.FileDescription) != projectRevision.DateTime)
    {
      lock (_Sync)
      {
        _Project = project;
      }
      throw new ArgumentException(nameof(projectRevision));
    }

    await project.DisposeAsync();

    _FileSystem.Copy(projectRevision.FileDescription, _Origin);
  }

  public Task RemoveRevisionAsync(ProjectRevision projectRevision, CancellationToken cancellationToken)
  {
    if (_FileSystem.GetLastWriteTime(projectRevision.FileDescription) != projectRevision.DateTime)
    {
      throw new ArgumentException(nameof(projectRevision));
    }

    return Task.Run(() => _FileSystem.RemoveFile(projectRevision.FileDescription), cancellationToken);
  }
}
