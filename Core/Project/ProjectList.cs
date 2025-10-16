using GT4.Utils;
using System.Data;

namespace GT4.Project;

internal class ProjectList : IProjectList
{
  private IStorage _Storage;
  private IFileSystem _FileSystem;
  private IReadOnlyList<ProjectItem>? _Items;

  private static async Task<ProjectItem> GetProjectItemAsync(string path)
  {
    try
    {
      await using var project = await ProjectDocument.OpenAsync(path);
      var results = await Task.WhenAll(
        project.GetMetadataAsync<string>("name"),
        project.GetMetadataAsync<string>("description"));

      return new ProjectItem
      {
        Name = results[0]
          ?? throw new DataException($"There is no name stored in the project ({path})"),
        Path = path,
        Description = results[1] ?? string.Empty
      };
    }
    catch (Exception ex)
    {
      //TODO BrokenProjectItem
      return new ProjectItem
      {
        Name = $"Error: {ex.Message}",
        Path = path
      };
    }
  }

  private static bool CompareNames(string name1, string name2) =>
    string.Equals(name1, name2, StringComparison.InvariantCultureIgnoreCase);

  private async Task<IReadOnlyList<ProjectItem>> LoadItemsAsync()
  {
    if (_Items is not null)
      return _Items;

    try
    {
      var tasks = _FileSystem
        .GetFiles(_Storage.ProjectsRoot, $"*.{ProjectExtension}", true)
        .ToList()
        .Select(GetProjectItemAsync);
      var result = await Task.WhenAll(tasks);

      return result;
    }
    catch
    {
      return new List<ProjectItem> { };
    }
  }

  private void InvalidateItems()
  {
    _Items = null;
  }

  public ProjectList(IStorage storage, IFileSystem fileSystem)
  {
    _Storage = storage;
    _FileSystem = fileSystem;
  }

  public readonly static string ProjectExtension = "gt4";

  public Task<IReadOnlyList<ProjectItem>> Items => LoadItemsAsync();

  public async Task CreateAsync(ProjectInfo info)
  {
    if ((await Items).Any(i => CompareNames(i.Name, info.Name)))
      throw new ApplicationException($"A project with name '{info.Name}' already exists");

    InvalidateItems();
    var path = Path.Combine(_Storage.ProjectsRoot, Guid.NewGuid().ToString(), $"project.{ProjectExtension}");
    using (var file = _FileSystem.CreateEmptyFile(path))
      file.Close();

    await using var project = await ProjectDocument.CreateNewAsync(path, info.Name);
    await Task.WhenAll(
      project.AddMetadataAsync("name", info.Name),
      project.AddMetadataAsync("description", info.Description));
  }

  public async Task RemoveAsync(string name)
  {
    var modifiableItems = (await Items).ToList();
    var item = modifiableItems.FirstOrDefault(i => CompareNames(i.Name, name));
    if (item?.Name is null)
      return;

    InvalidateItems();
    _FileSystem.RemoveDirectory(Path.GetDirectoryName(item.Path)!);
  }
}
