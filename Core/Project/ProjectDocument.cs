using Microsoft.Data.Sqlite;

namespace GT4.Project;

public class ProjectDocument : IAsyncDisposable, IDisposable
{
  readonly SqliteConnection _connection;

  static ProjectDocument()
  {
    SQLitePCL.Batteries.Init();
  }

  private ProjectDocument(string dbFilaName, SqliteOpenMode mode)
  {
    var builder = new SqliteConnectionStringBuilder();
    builder.Pooling = false;
    builder.DataSource = dbFilaName;
    builder.Mode = mode;
    var connectionString = builder.ConnectionString;
    _connection = new SqliteConnection(connectionString);
  }

  ~ProjectDocument()
  {
    Dispose();
  }

  private async Task OpenAsync()
  {
    await _connection.OpenAsync();
  }

  private async Task InitNewDB()
  {
    using var command = _connection.CreateCommand();
    command.CommandText = "CREATE TABLE IF NOT EXISTS Metadata (Id TEXT PRIMARY KEY, Data BLOB)";
    await command.ExecuteNonQueryAsync();
  }

  public async Task AddMetadataAsync<TData>(string id, TData data)
  {
    using var command = _connection.CreateCommand();
    command.CommandText = "INSERT OR REPLACE INTO Metadata (Id, Data) VALUES (@id, @data);";
    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@data", data);
    var rowsAffected = await command.ExecuteNonQueryAsync();
    Console.WriteLine(rowsAffected);
  }

  public async Task<TData?> GetMetadataAsync<TData>(string id)
  {
    using var command = _connection.CreateCommand();
    command.CommandText = "SELECT Data FROM Metadata WHERE Id=@id;";
    command.Parameters.Add(new SqliteParameter("@id", id));
    var result = await command.ExecuteScalarAsync();
    return (TData?)result;
  }

  public static async Task<ProjectDocument> CreateNewAsync(string path, string name)
  {
    var ret = new ProjectDocument(path, SqliteOpenMode.ReadWriteCreate);
    await ret.OpenAsync();
    await ret.InitNewDB();

    return ret;
  }

  public static async Task<ProjectDocument> OpenAsync(string path)
  {
    var ret = new ProjectDocument(path, SqliteOpenMode.ReadWrite);
    await ret.OpenAsync();

    return ret;
  }

  public async ValueTask DisposeAsync()
  {
    await _connection.CloseAsync();
    await _connection.DisposeAsync();
  }

  public void Dispose()
  {
    _connection.Close();
    _connection.Dispose();
  }
}
