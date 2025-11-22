using GT4.Core.Utils;

namespace GT4.Core.Project;

using IFileSystem = Utils.IFileSystem;

public class ProjectHost : IAsyncDisposable, IDisposable
{
  private readonly IFileSystem _FileSystem;
  private readonly FileDescription _Origin;
  private readonly FileDescription _Cache;
  private long? _ProjectRevision;
  private ProjectDocument? _Project = null;

  public ProjectHost(IFileSystem fileSystem, FileDescription origin, FileDescription cache)
  {
    _FileSystem = fileSystem;
    _Origin = origin;
    _Cache = cache;
    _FileSystem.Copy(_Origin, _Cache);
  }

  public ProjectDocument? Project 
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
    if (_Project is null || _Project.ProjectRevision == _ProjectRevision)
    {
      return;
    }
    var project = _Project;
    _Project = null;
    project.Dispose();
    _FileSystem.Copy(_Cache, _Origin);
    _FileSystem.RemoveFile(_Cache);
  }

  public async ValueTask DisposeAsync()
  {
    ProjectDocument project;
    lock (this)
    {
      if (_Project is null || _Project.ProjectRevision == _ProjectRevision)
      {
        return;
      }
      project = _Project;
      _Project = null;
    }

    await project.DisposeAsync();

    for (var i = 0; i < 5; i++)
    {
      try
      {
        _FileSystem.Copy(_Cache, _Origin);
        _FileSystem.RemoveFile(_Cache);
        break;
      }
      catch
      {
        var backoffInterval = TimeSpan.FromMilliseconds(100 + 200 * i);
        await Task.Delay(backoffInterval);
      }
    }
  }
}
