using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

public class ProjectDocument : IAsyncDisposable, IDisposable
{
  private readonly SqliteConnection _connection;

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

  private async Task InitNewDBAsync()
  {
    await Task.WhenAll(
      Metadata.CreateAsync(),
      Names.CreateAsync(),
      Persons.CreateAsync()
    );
  }

  public TableMetadata Metadata => new(this);
  public TableNames Names => new(this);
  public TablePersons Persons => new(this);

  public SqliteCommand CreateCommand()
  {
    return _connection.CreateCommand();
  }

  public static async Task<ProjectDocument> CreateNewAsync(string path, string name)
  {
    var ret = new ProjectDocument(path, SqliteOpenMode.ReadWriteCreate);
    await ret.OpenAsync();
    await ret.InitNewDBAsync();

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
