using GT4.Core.Project.Abstraction;
using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

internal class TableMetadata : TableBase, ITableMetadata
{
  private const string ProjectName = "name";
  private const string ProjectDescription = "description";
  private const string RevisionKey = "revision";

  public TableMetadata(IProjectDocument document) : base(document)
  {
  }

  internal override async Task CreateAsync(CancellationToken token)
  {
    using var command = Document.CreateCommand();
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
    var command = Document.CreateCommand();
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
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = CreateAddCommand(id, data);
    await command.ExecuteNonQueryAsync(token);
    await transaction.CommitAsync(token);
  }

  public async Task<TData?> GetAsync<TData>(string id, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = "SELECT Data FROM Metadata WHERE Id=@id;";
    command.Parameters.Add(new SqliteParameter("@id", id));
    var result = await command.ExecuteScalarAsync(token);
    return (TData?)result;
  }

  public Task<string?> GetProjectNameAsync(CancellationToken token) => GetAsync<string>(ProjectName, token);

  public Task<string?> GetProjectDescriptionAsync(CancellationToken token) => GetAsync<string>(ProjectDescription, token);

  public Task<string?> GetProjectRevisionAsync(CancellationToken token) => GetAsync<string>(RevisionKey, token);

  public Task SetProjectNameAsync(string value, CancellationToken token) => AddAsync(ProjectName, value, token);

  public Task SetProjectDescriptionAsync(string value, CancellationToken token) => AddAsync(ProjectDescription, value, token);

  public Task SetProjectRevisionAsync(string value, CancellationToken token) => AddAsync(RevisionKey, value, token);
  
  public void SetProjectRevision(string value)
  {
    using var command = CreateAddCommand(RevisionKey, value);
    command.ExecuteNonQuery();
  }
}
