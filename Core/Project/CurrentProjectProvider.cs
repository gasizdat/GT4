using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal class CurrentProjectProvider : ICurrentProjectProvider
{
  private readonly IProjectList _ProjectList;
  private ProjectInfo? _Info = null;
  private ProjectHost? _ProjectHost = null;

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

  public async Task CloseAsync(CancellationToken token)
  {
    ProjectHost projectHost;
    lock (this)
    {
      if (_ProjectHost is null)
      {
        return;
      }

      projectHost = _ProjectHost;
      _ProjectHost = null;
    }

    await projectHost.DisposeAsync();
  }

  public bool HasCurrentProject => _ProjectHost is not null;

  public ProjectDocument Project => _ProjectHost?.Project ?? throw new InvalidOperationException("Project is not opened yet.");

  public ProjectInfo Info => _Info ?? throw new InvalidOperationException("Project is not opened yet.");
}
