namespace GT4.Core.Utils;

public interface IStorage
{
  DirectoryDescription ProjectsCache { get; }
  DirectoryDescription ProjectsRoot { get; }
  DirectoryDescription AppConfig { get; }
}