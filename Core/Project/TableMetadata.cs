using GT4.Core.Project.Abstraction;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

internal class TableMetadata : TableBase, ITableMetadata
{
  private const string ProjectName = "name";
  private const string ProjectDescription = "description";
  private const string RevisionKey = "revision";

  public TableMetadata(IProjectConnection connection) : base(connection)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Connection.CreateCommand();
    command.CommandText = """
    CREATE TABLE IF NOT EXISTS Metadata (
      Id TEXT PRIMARY KEY, 
      Data BLOB
    );
    """;
    await command.ExecuteNonQueryAsync(token);
  }

  private ProjectCommand CreateAddCommand<TData>(string id, TData data)
  {
    var command = Connection.CreateCommand();
    command.CommandText = """
      INSERT OR REPLACE INTO Metadata 
          (Id, Data) 
          VALUES (@id, @data);
      """;
    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@data", data);
    return command;
  }

  public async Task AddAsync<TData>(string id, TData data, CancellationToken token)
  {
    using var transaction = await Connection.BeginTransactionAsync(token);
    using var command = CreateAddCommand(id, data);
    await command.ExecuteNonQueryAsync(token);
    await transaction.CommitAsync(token);
  }

  public async Task<TData?> GetAsync<TData>(string id, CancellationToken token)
  {
    using var command = Connection.CreateCommand();
    command.CommandText = "SELECT Data FROM Metadata WHERE Id=@id;";
    command.Parameters.Add(new SqliteParameter("@id", id));
    var result = await command.ExecuteScalarAsync(token);
    return (TData?)result;
  }

  /// <summary>
  /// Returns the values of every row whose key starts with <paramref name="prefix"/>, ordered by key for
  /// deterministic output. Used to read back a namespaced group of entries (e.g. GEDCOM passthrough
  /// records) that share a key prefix.
  /// </summary>
  public async Task<TData[]> GetByPrefixAsync<TData>(string prefix, CancellationToken token)
  {
    using var command = Connection.CreateCommand();
    command.CommandText = "SELECT Data FROM Metadata WHERE Id LIKE @prefix ORDER BY Id;";
    command.Parameters.AddWithValue("@prefix", prefix + "%");

    var results = new List<TData>();
    await using var reader = await command.ExecuteReaderAsync(token);
    while (await reader.ReadAsync(token))
    {
      results.Add((TData)reader.GetValue(0));
    }
    return [.. results];
  }

  public Task<string?> GetProjectNameAsync(CancellationToken token) => GetAsync<string>(ProjectName, token);

  public Task<string?> GetProjectDescriptionAsync(CancellationToken token) => GetAsync<string>(ProjectDescription, token);

  public async Task<long?> GetProjectRevisionAsync(CancellationToken token) =>
    await GetAsync<object>(RevisionKey, token) as long? ?? 0;

  public Task SetProjectNameAsync(string value, CancellationToken token) => AddAsync(ProjectName, value, token);

  public Task SetProjectDescriptionAsync(string value, CancellationToken token) => AddAsync(ProjectDescription, value, token);

  public long UpdateProjectRevision()
  {
    // A first commit (no row) or a legacy string value is folded in by the CAST, migrating it forward.
    using var command = Connection.CreateCommand();
    command.CommandText = """
      INSERT INTO Metadata (Id, Data) VALUES (@id, 1)
      ON CONFLICT(Id) DO UPDATE SET Data = CAST(Data AS INTEGER) + 1
      RETURNING Data;
      """;
    command.Parameters.AddWithValue("@id", RevisionKey);
    return Convert.ToInt64(command.ExecuteScalar());
  }
}
