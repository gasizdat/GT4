using GT4.Core.Utils;
using System.Data;

namespace GT4.Core.Project;

internal class ProjectList : IProjectList
{
  private readonly IStorage _Storage;
  private readonly IFileSystem _FileSystem;
  private readonly WeakReference<ProjectItem[]?> _Items = new(null);

  private static async Task<ProjectItem> GetProjectItemAsync(string path)
  {
    try
    {
      await using var project = await ProjectDocument.OpenAsync(path);
      var results = await Task.WhenAll(
        project.Metadata.GetAsync<string>("name"),
        project.Metadata.GetAsync<string>("description"));

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

  private async Task<ProjectItem[]> LoadItemsAsync()
  {
    if (_Items.TryGetTarget(out var items))
      return items;

    try
    {
      var tasks = _FileSystem
        .GetFiles(_Storage.ProjectsRoot, $"*.{ProjectExtension}", true)
        .ToList()
        .Select(GetProjectItemAsync);
      items = await Task.WhenAll(tasks);

      _Items.SetTarget(items);
      return items;
    }
    catch
    {
      return Array.Empty<ProjectItem>();
    }
  }

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

  public Task<ProjectItem[]> Items => LoadItemsAsync();

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
      project.Metadata.AddAsync("name", info.Name),
      project.Metadata.AddAsync("description", info.Description));
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
