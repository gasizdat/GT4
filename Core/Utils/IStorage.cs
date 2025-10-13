namespace GT4.Utils;

public interface IStorage
{
  string ApplicationData { get; }
  string ProjectListPath { get; }
  string ProjectsRoot { get; }
}