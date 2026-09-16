using System.Net.Mime;

namespace GT4.Core.Utils;

// Backs one project's own config file under IStorage.ProjectsCache -- unlike AppConfigurationProvider's
// single global file, a fresh instance is built per project via Factory, so it is never registered as a
// ConfigurationRoot provider; callers must Load() and Flush() around each use themselves.
public sealed class ProjectConfigurationProvider : FlatJsonConfigurationProvider
{
  public record class Factory(IFileSystem FileSystem, IStorage Storage)
  {
    public ProjectConfigurationProvider Create(FileDescription origin) => new(FileSystem, Storage, origin);
  }

  private readonly IStorage _Storage;
  private readonly FileDescription _Origin;

  private ProjectConfigurationProvider(IFileSystem fileSystem, IStorage storage, FileDescription origin) : base(fileSystem)
  {
    _Storage = storage;
    _Origin = origin;
  }

  public override string Name => WellKnownActiveConfigurations.ProjectConfig;

  // Matches ProjectList.GetCacheFileDescription's own per-project folder, which already exists by
  // the time any project is listed: same basename-without-extension collision tradeoff as that.
  protected override FileDescription File
  {
    get
    {
      var projectDir = _Storage.ProjectsCache with
      {
        Path = [.. _Storage.ProjectsCache.Path, Path.GetFileNameWithoutExtension(_Origin.FileName)]
      };
      return new FileDescription(projectDir, "projectconfig.json", MediaTypeNames.Text.Plain);
    }
  }
}
