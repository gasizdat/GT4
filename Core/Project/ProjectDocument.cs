using Microsoft.Data.Sqlite;
using System.Data;
using System.Reflection.Metadata;

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

  private async Task OpenAsync(CancellationToken token)
  {
    await _connection.OpenAsync(token);
  }

  private async Task InitNewDBAsync(CancellationToken token)
  {
    using var transaction = await BeginTransactionAsync(token);

    await Task.WhenAll(
      Metadata.CreateAsync(token),
      Names.CreateAsync(token),
      Persons.CreateAsync(token),
      PersonNames.CreateAsync(token)
    );

    transaction.Commit();
  }

  public TableMetadata Metadata => new(this);
  public TableNames Names => new(this);
  public TablePersons Persons => new(this);
  public TablePersonNames PersonNames => new(this);

  public async Task<int> GetLastInsertRowIdAsync(CancellationToken token)
  {
    using var command = CreateCommand();
    command.CommandText = "SELECT last_insert_rowid();";
    return Convert.ToInt32(await command.ExecuteScalarAsync(token));
  }
  
  public SqliteCommand CreateCommand()
  {
    return _connection.CreateCommand();
  }

  public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken token)
  {
    return await _connection.BeginTransactionAsync(token);
  }

  public static async Task<ProjectDocument> CreateNewAsync(string path, string name, CancellationToken token)
  {
    var ret = new ProjectDocument(path, SqliteOpenMode.ReadWriteCreate);
    await ret.OpenAsync(token);
    await ret.InitNewDBAsync(token);

    return ret;
  }

  public static async Task<ProjectDocument> OpenAsync(string path, CancellationToken token)
  {
    var ret = new ProjectDocument(path, SqliteOpenMode.ReadWrite);
    await ret.OpenAsync(token);

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
