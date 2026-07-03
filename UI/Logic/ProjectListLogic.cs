using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.Text;

namespace GT4.UI.Logic;

public class ProjectListLogic
{
  private readonly IProjectList _ProjectList;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<ProjectInfo> _ProjectInfoComparer;
  private readonly IGedcomImporter _Importer;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;

  public ProjectListLogic(
    IProjectList projectList,
    ICurrentProjectProvider currentProjectProvider,
    IComparer<ProjectInfo> projectInfoComparer,
    IGedcomImporter importer,
    ICancellationTokenProvider cancellationTokenProvider)
  {
    _ProjectList = projectList;
    _CurrentProjectProvider = currentProjectProvider;
    _ProjectInfoComparer = projectInfoComparer;
    _Importer = importer;
    _CancellationTokenProvider = cancellationTokenProvider;
  }

  public async Task<ProjectInfo[]> GetProjectsAsync()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var projects = await _ProjectList.GetItemsAsync(token);
    return [.. projects.OrderBy(p => p, _ProjectInfoComparer)];
  }

  public async Task CreateProjectAsync(string name, string description)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await using var _ = await _ProjectList.CreateAsync(name, description, token);
  }

  public async Task CloseCurrentAsync()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider.CloseAsync(token);
  }

  public async Task OpenAsync(ProjectInfo info)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider.OpenAsync(info, token);
  }

  public async Task<ProjectInfo> ImportAsync(Stream content, string name, string description, string? mediaBasePath, CancellationToken token)
  {
    var host = await _ProjectList.CreateAsync(name, description, token);
    try
    {
      using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
      await _Importer.ImportAsync(host.Project!, reader, token, mediaBasePath);

      var revision = await host.Project!.Metadata.GetProjectRevisionAsync(token) ?? string.Empty;
      await host.DisposeAsync();
      return new ProjectInfo(Revision: revision, Description: description, Name: name, Origin: host.Origin);
    }
    catch
    {
      await host.DisposeAsync();
      using var cleanupToken = _CancellationTokenProvider.CreateDbCancellationToken();
      await _ProjectList.RemoveAsync(host.Origin, cleanupToken);
      throw;
    }
  }
}
