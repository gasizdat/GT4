namespace GT4.Core.Project.Abstraction;

public interface ITableMetadata
{
  Task AddAsync<TData>(string id, TData data, CancellationToken token);
  Task<TData?> GetAsync<TData>(string id, CancellationToken token);
  Task<string?> GetProjectDescriptionAsync(CancellationToken token);
  Task<string?> GetProjectNameAsync(CancellationToken token);
  Task<string?> GetProjectRevisionAsync(CancellationToken token);
  Task SetProjectDescriptionAsync(string value, CancellationToken token);
  Task SetProjectNameAsync(string value, CancellationToken token);
  Task SetProjectRevisionAsync(string value, CancellationToken token);
}