namespace GT4.Core.Project;

internal class CurrentProjectProvider : ICurrentProjectProvider
{
  private ProjectItem? _item = null;
  private ProjectDocument? _project = null;

  public async Task OpenAsync(ProjectItem item, CancellationToken token)
  {
    await CloseAsync(token);
    _item = item;
    _project = await ProjectDocument.OpenAsync(item.Path, token);
  }

  public async Task CloseAsync(CancellationToken token)
  {
    if (_project is not null)
    {
      await _project.DisposeAsync();
      _item = null;
      _project = null;
    }
  }

  public bool HasCurrentProject => _project is not null;

  public ProjectDocument Project => _project ?? throw new InvalidOperationException("Project is not opened yet.");

  public ProjectItem Item => _item ?? throw new InvalidOperationException("Project is not opened yet.");
}
