namespace GT4.Core.Project.Abstraction;

public interface ITableMetadata
{
  Task AddAsync<TData>(string id, TData data, CancellationToken token);
  Task<TData?> GetAsync<TData>(string id, CancellationToken token);
  Task<TData[]> GetByPrefixAsync<TData>(string prefix, CancellationToken token);
  Task<string?> GetProjectDescriptionAsync(CancellationToken token);
  Task<string?> GetProjectNameAsync(CancellationToken token);
  Task<long?> GetProjectRevisionAsync(CancellationToken token);
  Task SetProjectDescriptionAsync(string value, CancellationToken token);
  Task SetProjectNameAsync(string value, CancellationToken token);

  // Synchronous by design: NestedTransaction.CommitAsync stamps the revision on commit and must not
  // await here, or the AsyncLocal ambient transaction would be stranded on a continuation.
  internal long UpdateProjectRevision();
}