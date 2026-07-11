namespace GT4.Core.Project.Abstraction;

public interface IProjectDocumentFactory
{
  Task<IProjectDocument> OpenAsync(string path, CancellationToken token);
  Task<IProjectDocument> CreateNewAsync(string path, string name, CancellationToken token);
}
