using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public class TableMetadata : TableBase
{
  public TableMetadata(ProjectDocument document) : base(document)
  {
  }

  public override async Task CreateAsync()
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
    CREATE TABLE IF NOT EXISTS Metadata (
      Id TEXT PRIMARY KEY, 
      Data BLOB
    );
    """;
    await command.ExecuteNonQueryAsync();
  }

  public async Task AddAsync<TData>(string id, TData data)
  {
    using var command = Document.CreateCommand();
    command.CommandText = """
      INSERT OR REPLACE INTO Metadata 
          (Id, Data) 
          VALUES (@id, @data);
      """;
    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@data", data);
    var rowsAffected = await command.ExecuteNonQueryAsync();
    Console.WriteLine(rowsAffected);
  }

  public async Task<TData?> GetAsync<TData>(string id)
  {
    using var command = Document.CreateCommand();
    command.CommandText = "SELECT Data FROM Metadata WHERE Id=@id;";
    command.Parameters.Add(new SqliteParameter("@id", id));
    var result = await command.ExecuteScalarAsync();
    return (TData?)result;
  }
}
