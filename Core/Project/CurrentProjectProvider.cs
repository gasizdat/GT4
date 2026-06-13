using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using System.Diagnostics.CodeAnalysis;

namespace GT4.Core.Project;

internal class CurrentProjectProvider : ICurrentProjectProvider
{
  private readonly IProjectList _ProjectList;
  // Guards _Info and _ProjectHost. A dedicated object (not `this`) so callers can't accidentally
  // contend on the same monitor, and held only for the brief field reads/writes — never across an
  // await, so the document I/O it guards access to is not serialized by it.
  private readonly object _Sync = new();
  private ProjectInfo? _Info = null;
  private ProjectHost? _ProjectHost = null;

  [DoesNotReturn]
  private T ThrowProjectNotOpened<T>()
  {
    throw new ProjectNotOpenedException();
  }

  public CurrentProjectProvider(IProjectList projectList)
  {
    _ProjectList = projectList;
  }

  /// <summary>Returns the currently open host, or throws if no project is open.</summary>
  private ProjectHost RequireHost()
  {
    lock (_Sync)
    {
      return _ProjectHost?.Project is not null ? _ProjectHost : ThrowProjectNotOpened<ProjectHost>();
    }
  }

  public async Task OpenAsync(ProjectInfo info, CancellationToken token)
  {
    await CloseAsync(token);
    var host = await _ProjectList.OpenAsync(origin: info.Origin, token);
    lock (_Sync)
    {
      _Info = info;
      _ProjectHost = host;
    }
  }

  public async Task UpdateOriginAsync(CancellationToken token)
  {
    var info = Info;
    await CloseAsync(token);
    await OpenAsync(info, token);
  }

  public async Task CloseAsync(CancellationToken token)
  {
    ProjectHost? projectHost;
    lock (_Sync)
    {
      projectHost = _ProjectHost;
      _ProjectHost = null;
    }

    if (projectHost is not null)
    {
      await projectHost.DisposeAsync();
    }
  }

  public Task RemoveRevisionAsync(ProjectRevision revision, CancellationToken cancellationToken) =>
    RequireHost().RemoveRevisionAsync(revision, cancellationToken);

  public async Task RestoreRevisionAsync(ProjectRevision revision, CancellationToken cancellationToken)
  {
    var host = RequireHost();
    await host.RestoreRevisionAsync(revision, cancellationToken);
    await host.DisposeAsync();

    ProjectInfo info;
    lock (_Sync)
    {
      info = _Info ?? ThrowProjectNotOpened<ProjectInfo>();
    }

    var reopened = await _ProjectList.OpenAsync(origin: info.Origin, cancellationToken);
    lock (_Sync)
    {
      _ProjectHost = reopened;
    }
  }

  public bool HasCurrentProject
  {
    get
    {
      lock (_Sync)
      {
        return _ProjectHost?.Project is not null;
      }
    }
  }

  public IProjectDocument Project
  {
    get
    {
      lock (_Sync)
      {
        return _ProjectHost?.Project ?? ThrowProjectNotOpened<IProjectDocument>();
      }
    }
  }

  public ProjectInfo Info
  {
    get
    {
      lock (_Sync)
      {
        return _Info ?? ThrowProjectNotOpened<ProjectInfo>();
      }
    }
  }

  public ICollection<ProjectRevision> Revisions
  {
    // Snapshot the host under the lock, then enumerate revisions (which touches the filesystem)
    // outside it so the lock is not held during I/O.
    get => RequireHost().Revisions;
  }
}
