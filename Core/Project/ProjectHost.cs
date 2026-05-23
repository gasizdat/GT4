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
    get => _Project;
    set
    {
      _Project = value;
      _ProjectRevision = _Project?.ProjectRevision;
    }
  }

  public void Dispose()
  {
    if (_Project is null)
    {
      return;
    }
    var project = _Project;
    _Project = null;
    var actualRevision = project.ProjectRevision;
    project.Dispose();
    if (actualRevision != _ProjectRevision)
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
    lock (this)
    {
      if (_Project is null)
      {
        return;
      }
      project = _Project;
      _Project = null;
    }

    var actualRevision = project.ProjectRevision;
    await project.DisposeAsync();

    for (var i = 0; i < 5; i++)
    {
      try
      {
        if (actualRevision != _ProjectRevision)
        {
          _FileSystem.Copy(_Cache, _Origin);
          actualRevision = _ProjectRevision ?? 0;
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
    .ToList();

  public async Task RestoreRevisionAsync(ProjectRevision projectRevision, CancellationToken cancellationToken)
  {
    if (_Project is null)
    {
      throw new ApplicationException("No project is set");
    }
    if (_FileSystem.GetLastWriteTime(projectRevision.FileDescription) != projectRevision.DateTime)
    {
      throw new ArgumentException(nameof(projectRevision));
    }

    await _Project.DisposeAsync();
    _Project = null;

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
