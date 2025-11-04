namespace GT4.Core.Utils;

public interface IStorage
{
  DirectoryDescription ApplicationData { get; }
  DirectoryDescription ProjectsRoot { get; }
}