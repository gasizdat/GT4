using GT4.Core.Utils;

namespace GT4.Core.Project;

public class ProjectHost : IAsyncDisposable, IDisposable
{
  private readonly IFileSystem _FileSystem;
  private readonly FileDescription _Origin;
  private readonly FileDescription _Cache;

  public ProjectHost(IFileSystem fileSystem, FileDescription origin, FileDescription cache)
  {
    _FileSystem = fileSystem;
    _Origin = origin;
    _Cache = cache;
    _FileSystem.Copy(_Origin, _Cache);
  }

  public ProjectDocument? Project { get; set; } = null;

  public void Dispose()
  {
    if (Project is null)
    {
      return;
    }
    var project = Project;
    Project = null;
    project.Dispose();
    _FileSystem.Copy(_Cache, _Origin);
    _FileSystem.RemoveFile(_Cache);
  }

  public ValueTask DisposeAsync()
  {
    ProjectDocument project;
    lock (this)
    {
      if (Project is null)
      {
        return ValueTask.CompletedTask;
      }
      project = Project;
      Project = null;
    }

    var ret = project.DisposeAsync();
    _FileSystem.Copy(_Cache, _Origin);
    _FileSystem.RemoveFile(_Cache);

    return ret;
  }
}
