using GT4.Utils;
using System.Data;

namespace GT4.Project;

internal class ProjectList : IProjectList
{
  private IStorage _Storage;
  private IFileSystem _FileSystem;

  private async Task<ProjectItem> GetProjectItemAsync(string path)
  {
    try
    {
      await using var project = await ProjectDoc.OpenAsync(path);
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

  private async Task<IReadOnlyList<ProjectItem>> LoadItemsAsync()
  {
    try
    {
      var tasks = _FileSystem.GetFiles(_Storage.ProjectsRoot, "*.db", true)
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

  private static bool CompareNames(string name1, string name2) =>
    string.Equals(name1, name2, StringComparison.InvariantCultureIgnoreCase);

  public ProjectList(IStorage storage, IFileSystem fileSystem)
  {
    _Storage = storage;
    _FileSystem = fileSystem;
  }

  public Task<IReadOnlyList<ProjectItem>> Items => LoadItemsAsync();

  public async Task CreateAsync(string name, string description)
  {
    if ((await Items).Any(i => CompareNames(i.Name, name)))
      throw new ApplicationException($"A project with name '{name}' already exists");

    var path = Path.Combine(_Storage.ProjectsRoot, Guid.NewGuid().ToString(), "project.db");
    using var file = _FileSystem.CreateEmptyFile(path);

    await using var project = await ProjectDoc.CreateNewAsync(file, name);
    await Task.WhenAll(
      project.AddMetadataAsync("name", name),
      project.AddMetadataAsync("description", description));
  }

  public async Task RemoveAsync(string name)
  {
    var modifiableItems = (await Items).ToList();
    var item = modifiableItems.FirstOrDefault(i => CompareNames(i.Name, name));
    if (item.Name is null)
      return;

    await Task.Run(() => _FileSystem.RemoveFile(item.Path));
  }
}
