using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

internal class CurrentProjectProvider : ICurrentProjectProvider
{
  private ProjectInfo? _Info = null;
  private ProjectDocument? _Project = null;

  public async Task OpenAsync(ProjectInfo info, CancellationToken token)
  {
    await CloseAsync(token);
    _Info = info;
    _Project = await ProjectDocument.OpenAsync(info.Path, token);
  }

  public async Task CloseAsync(CancellationToken token)
  {
    if (_Project is not null)
    {
      await _Project.DisposeAsync();
      _Project = null;
    }
  }

  public bool HasCurrentProject => _Project is not null;

  public ProjectDocument Project => _Project ?? throw new InvalidOperationException("Project is not opened yet.");

  public ProjectInfo Info => _Info ?? throw new InvalidOperationException("Project is not opened yet.");
}
