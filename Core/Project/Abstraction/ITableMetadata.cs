namespace GT4.Core.Project.Abstraction;

public interface ITableMetadata
{
  Task AddAsync<TData>(string id, TData data, CancellationToken token);
  Task<TData?> GetAsync<TData>(string id, CancellationToken token);
  Task<string?> GetProjectDescription(CancellationToken token);
  Task<string?> GetProjectName(CancellationToken token);
  Task SetProjectDescription(string value, CancellationToken token);
  Task SetProjectName(string value, CancellationToken token);
}