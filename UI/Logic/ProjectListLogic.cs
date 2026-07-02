using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Text;

namespace GT4.UI.Logic;

public class ProjectListLogic
{
  private readonly IProjectList _projectList;
  private readonly ICurrentProjectProvider _currentProjectProvider;
  private readonly IComparer<ProjectInfo> _projectInfoComparer;
  private readonly IGedcomImporter _importer;
  private readonly ICancellationTokenProvider _cancellationTokenProvider;

  public ProjectListLogic(
    IProjectList projectList,
    ICurrentProjectProvider currentProjectProvider,
    IComparer<ProjectInfo> projectInfoComparer,
    IGedcomImporter importer,
    ICancellationTokenProvider cancellationTokenProvider)
  {
    _projectList = projectList;
    _currentProjectProvider = currentProjectProvider;
    _projectInfoComparer = projectInfoComparer;
    _importer = importer;
    _cancellationTokenProvider = cancellationTokenProvider;
  }

  public async Task<ProjectInfo[]> GetProjectsAsync()
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    var projects = await _projectList.GetItemsAsync(token);
    return [.. projects.OrderBy(p => p, _projectInfoComparer)];
  }

  public async Task CreateProjectAsync(string name, string description)
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    await using var _ = await _projectList.CreateAsync(name, description, token);
  }

  public async Task CloseCurrentAsync()
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    await _currentProjectProvider.CloseAsync(token);
  }

  public async Task OpenAsync(ProjectInfo info)
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    await _currentProjectProvider.OpenAsync(info, token);
  }

  public async Task<ProjectInfo> ImportAsync(Stream content, string name, string description, string? mediaBasePath, CancellationToken token)
  {
    var host = await _projectList.CreateAsync(name, description, token);
    try
    {
      using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
      await _importer.ImportAsync(host.Project!, reader, token, mediaBasePath);

      var revision = await host.Project!.Metadata.GetProjectRevisionAsync(token) ?? string.Empty;
      await host.DisposeAsync();
      return new ProjectInfo(Revision: revision, Description: description, Name: name, Origin: host.Origin);
    }
    catch
    {
      await host.DisposeAsync();
      using var cleanupToken = _cancellationTokenProvider.CreateDbCancellationToken();
      await _projectList.RemoveAsync(host.Origin, cleanupToken);
      throw;
    }
  }
}
