using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using System.Diagnostics.CodeAnalysis;

namespace GT4.Core.Project;

internal class CurrentProjectProvider : ICurrentProjectProvider
{
  private readonly IProjectList _ProjectList;
  private ProjectInfo? _Info = null;
  private ProjectHost? _ProjectHost = null;

  [DoesNotReturn]
  private T ThrowProjectNotOpened<T>()
  {
    throw new InvalidOperationException("Project is not opened yet.");
  }

  public CurrentProjectProvider(IProjectList projectList)
  {
    _ProjectList = projectList;
  }

  public async Task OpenAsync(ProjectInfo info, CancellationToken token)
  {
    await CloseAsync(token);
    _Info = info;
    _ProjectHost = await _ProjectList.OpenAsync(origin: info.Origin, token);
  }

  public async Task UpdateOriginAsync(CancellationToken token)
  {
    var info = Info;
    await CloseAsync(token);
    await OpenAsync(info, token);
  }

  public async Task CloseAsync(CancellationToken token)
  {
    if (_ProjectHost is null)
    {
      return;
    }

    await _ProjectHost.DisposeAsync();
    _ProjectHost = null;
  }

  public async Task RemoveRevisionAsync(ProjectRevision revision, CancellationToken cancellationToken)
  {
    if (!HasCurrentProject)
    {
      ThrowProjectNotOpened<bool>();
    }

    await _ProjectHost.RemoveRevisionAsync(revision, cancellationToken);
  }

  public async Task RestoreRevisionAsync(ProjectRevision revision, CancellationToken cancellationToken)
  {
    if (!HasCurrentProject)
    {
      ThrowProjectNotOpened<bool>();
    }

    await _ProjectHost.RestoreRevisionAsync(revision, cancellationToken);
    await _ProjectHost.DisposeAsync();

    _ProjectHost = await _ProjectList.OpenAsync(origin: _Info!.Origin, cancellationToken);
  }

  [MemberNotNullWhen(true, nameof(_ProjectHost))]
  public bool HasCurrentProject => _ProjectHost?.Project is not null;

  public IProjectDocument Project => _ProjectHost?.Project ?? ThrowProjectNotOpened<IProjectDocument>();

  public ProjectInfo Info => _Info ?? ThrowProjectNotOpened<ProjectInfo>();

  public ICollection<ProjectRevision> Revisions =>
    _ProjectHost?.Revisions ?? ThrowProjectNotOpened<ICollection<ProjectRevision>>();
}
