using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public class TableMetadata : TableBase
{
  public TableMetadata(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync(CancellationToken token)
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

  public async Task AddAsync<TData>(string id, TData data, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT OR REPLACE INTO Metadata 
          (Id, Data) 
          VALUES (@id, @data);
      """;
    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@data", data);
    await command.ExecuteNonQueryAsync(token);
    transaction.Commit();
  }

  public async Task<TData?> GetAsync<TData>(string id, CancellationToken token)
  {
    using var command = Document.CreateCommand();
    command.CommandText = "SELECT Data FROM Metadata WHERE Id=@id;";
    command.Parameters.Add(new SqliteParameter("@id", id));
    var result = await command.ExecuteScalarAsync(token);
    return (TData?)result;
  }
}
