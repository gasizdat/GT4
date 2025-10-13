using System.Text.Json;

namespace GT4.Utils;

internal class ProjectList : IDisposable, IProjectList
{
  private IStorage _Storage;
  private IFileSystem _FileSystem;
  private IReadOnlyList<ProjectItem>? _Items;

  private IReadOnlyList<ProjectItem> LoadItems()
  {
    try
    {
      var doc = _FileSystem.ReadJsonFile(_Storage.ProjectListPath);
      return JsonSerializer.Deserialize<List<ProjectItem>>(doc) ?? new List<ProjectItem> { };
    }
    catch
    {
      return new List<ProjectItem> { };
    }
  }

  private void StoreItems()
  {
    _FileSystem.WriteJsonFile(_Storage.ProjectListPath, JsonSerializer.SerializeToDocument(Items));
  }

  private static bool CompareNames(string name1, string name2) =>
    string.Equals(name1, name2, StringComparison.InvariantCultureIgnoreCase);

  public ProjectList(IStorage storage, IFileSystem fileSystem)
  {
    _Storage = storage;
    _FileSystem = fileSystem;
  }

  ~ProjectList()
  {
    Dispose();
  }

  public IReadOnlyList<ProjectItem> Items { get => _Items ??= LoadItems(); }

  public void Add(string name, string path)
  {
    if (Items.Any(i => CompareNames(i.Name, name)))
      throw new ApplicationException($"A project with name '{name}' already exists");

    var modifiableItems = Items.ToList();
    modifiableItems.Add(new ProjectItem { Name = name, Path = path });
    _Items = modifiableItems;
  }

  public void Remove(string name)
  {
    var modifiableItems = Items.ToList();
    var item = modifiableItems.FirstOrDefault(i => CompareNames(i.Name, name));
    if (item.Name is null)
      return;

    modifiableItems.Remove(item);
    _Items = modifiableItems;
  }

  public void Dispose()
  {
    StoreItems();
  }
}
