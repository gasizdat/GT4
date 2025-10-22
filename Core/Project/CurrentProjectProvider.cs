namespace GT4.Core.Project;

internal class CurrentProjectProvider : ICurrentProjectProvider
{
  private ProjectItem? _Item = null;
  private ProjectDocument? _Project = null;

  public async Task OpenAsync(ProjectItem item, CancellationToken token)
  {
    await CloseAsync(token);
    _Item = item;
    _Project = await ProjectDocument.OpenAsync(item.Path, token);
  }

  public async Task CloseAsync(CancellationToken token)
  {
    if (_Project is not null)
    {
      await _Project.DisposeAsync();
      _Item = null;
      _Project = null;
    }
  }

  public bool HasCurrentProject => _Project is not null;

  public ProjectDocument Project => _Project ?? throw new InvalidOperationException("Project is not opened yet.");

  public ProjectItem Item => _Item ?? throw new InvalidOperationException("Project is not opened yet.");
}
