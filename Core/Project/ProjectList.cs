using GT4.Utils;

namespace GT4.Project;

internal class ProjectList : IProjectList
{
  private IStorage _Storage;
  private IFileSystem _FileSystem;
  private IReadOnlyList<ProjectItem>? _Items;

  private IReadOnlyList<ProjectItem> LoadItems()
  {
    try
    {
      var ret = new List<ProjectItem>();
      foreach (var path in _FileSystem.GetFiles(_Storage.ProjectsRoot, "*.db", true))
      {
        ret.Add(new ProjectItem
        {
          Name = Path.GetFileNameWithoutExtension(path),
          Path = path
        });
      }
      return ret;
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

  public IReadOnlyList<ProjectItem> Items { get => _Items ??= LoadItems(); }

  public void Create(string name)
  {
    if (Items.Any(i => CompareNames(i.Name, name)))
      throw new ApplicationException($"A project with name '{name}' already exists");

    var path = Path.Combine(_Storage.ProjectsRoot, Guid.NewGuid().ToString(), "project.db");
    using var file = _FileSystem.CreateEmptyFile(path);
    ProjectDoc.CreateNew(file, name);
  }

  public void Remove(string name)
  {
    var modifiableItems = Items.ToList();
    var item = modifiableItems.FirstOrDefault(i => CompareNames(i.Name, name));
    if (item.Name is null)
      return;

    _FileSystem.RemoveFile(item.Path);
  }
}
