using GT4.Core.Utils;
using GT4.Core.Project.Dto;

using System.Data;

namespace GT4.Core.Project;

internal class ProjectList : IProjectList
{
  private readonly IStorage _Storage;
  private readonly IFileSystem _FileSystem;
  private readonly WeakReference<ProjectInfo[]?> _Items = new(null);

  private static async Task<ProjectInfo> GetProjectItemAsync(string path, CancellationToken token)
  {
    try
    {
      await using var project = await ProjectDocument.OpenAsync(path, token);
      var results = await Task.WhenAll(
        project.Metadata.GetAsync<string>("name", token),
        project.Metadata.GetAsync<string>("description", token));

      return new ProjectInfo(
        Description: results[1] ?? string.Empty,
        Name : results[0]
          ?? throw new DataException($"There is no name stored in the project ({path})"),
        Path: path
      );
    }
    catch (Exception ex)
    {
      //TODO BrokenProjectItem
      return new ProjectInfo (
        Description: ex.ToString(),
        Name: $"Error: {ex.Message}",
        Path: path
      );
    }
  }

  private static bool CompareNames(string name1, string name2) =>
    string.Equals(name1, name2, StringComparison.InvariantCultureIgnoreCase);

  private void InvalidateItems()
  {
    _Items.SetTarget(null);
  }

  public ProjectList(IStorage storage, IFileSystem fileSystem)
  {
    _Storage = storage;
    _FileSystem = fileSystem;
  }

  public readonly static string ProjectExtension = "gt4";

  public async Task<ProjectInfo[]> GetItemsAsync(CancellationToken token)
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    try
    {
      var tasks = _FileSystem
        .GetFiles(_Storage.ProjectsRoot, $"*.{ProjectExtension}", true)
        .ToList()
        .Select(path => GetProjectItemAsync(path, token));
      items = await Task.WhenAll(tasks);

      _Items.SetTarget(items);
      return items;
    }
    catch
    {
      return Array.Empty<ProjectInfo>();
    }
  }

  public async Task CreateAsync(ProjectInfo info, CancellationToken token)
  {
    if ((await GetItemsAsync(token)).Any(i => CompareNames(i.Name, info.Name)))
      throw new ApplicationException($"A project with name '{info.Name}' already exists");

    InvalidateItems();
    var path = Path.Combine(_Storage.ProjectsRoot, Guid.NewGuid().ToString(), $"project.{ProjectExtension}");
    using (var file = _FileSystem.CreateEmptyFile(path))
      file.Close();

    await using var project = await ProjectDocument.CreateNewAsync(path, info.Name, token);
    await Task.WhenAll(
      project.Metadata.AddAsync("name", info.Name, token),
      project.Metadata.AddAsync("description", info.Description, token));
  }

  public async Task RemoveAsync(string name, CancellationToken token)
  {
    var modifiableItems = (await GetItemsAsync(token)).ToList();
    var item = modifiableItems.FirstOrDefault(i => CompareNames(i.Name, name));
    if (item?.Name is null)
      return;

    InvalidateItems();
    _FileSystem.RemoveDirectory(Path.GetDirectoryName(item.Path)!);
  }
}
