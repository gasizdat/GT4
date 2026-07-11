using GT4.Core.Project.Abstraction;

namespace GT4.Core.Project;

internal sealed class ProjectDocumentFactory : IProjectDocumentFactory
{
  public async Task<IProjectDocument> OpenAsync(string path, CancellationToken token) =>
    await ProjectDocument.OpenAsync(path, token);

  public async Task<IProjectDocument> CreateNewAsync(string path, string name, CancellationToken token) =>
    await ProjectDocument.CreateNewAsync(path, name, token);
}
