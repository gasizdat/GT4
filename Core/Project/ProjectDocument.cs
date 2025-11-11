using Microsoft.Data.Sqlite;
using System.Data;

namespace GT4.Core.Project;

public class ProjectDocument : IAsyncDisposable, IDisposable
{
  private readonly SqliteConnection _Connection;
  private NestedTransaction? _CurrentTransaction = null;
  private int _TransactionNo = 0;

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
    _Connection = new SqliteConnection(connectionString);
  }

  ~ProjectDocument()
  {
    Dispose();
  }

  private async Task OpenAsync(CancellationToken token)
  {
    await _Connection.OpenAsync(token);
  }

  private async Task InitNewDBAsync(CancellationToken token)
  {
    using var transaction = await BeginTransactionAsync(token);

    await Task.WhenAll(
      Metadata.CreateAsync(token),
      Names.CreateAsync(token),
      Persons.CreateAsync(token),
      PersonNames.CreateAsync(token),
      Data.CreateAsync(token),
      Relatives.CreateAsync(token),
      PersonData.CreateAsync(token)
    );

    transaction.Commit();
  }

  public const string MimeType = "application/gt4;storage=sqlite";

  public TableMetadata Metadata => new(this);
  public TableNames Names => new(this);
  public TablePersons Persons => new(this);
  public TablePersonNames PersonNames => new(this);
  public TableData Data => new(this);
  public TableRelatives Relatives => new(this);
  public TablePersonData PersonData => new(this);

  public FamilyManager FamilyManager => new(this);
  public PersonManager PersonManager => new(this);

  public async Task<int> GetLastInsertRowIdAsync(CancellationToken token)
  {
    using var command = CreateCommand();
    command.CommandText = "SELECT last_insert_rowid();";
    return Convert.ToInt32(await command.ExecuteScalarAsync(token));
  }

  public SqliteCommand CreateCommand()
  {
    return _Connection.CreateCommand();
  }

  public Task<IDbTransaction> BeginTransactionAsync(CancellationToken token)
  {
    IDbTransaction ret;

    lock (this)
    {
      if (_CurrentTransaction is not null && !_CurrentTransaction.IsDisposed)
      {
        var transactionName = $"InnerTransaction_{Interlocked.Increment(ref _TransactionNo)}";
        ret = new NestedTransaction(_CurrentTransaction, transactionName);
      }
      else
      {
        ret = _CurrentTransaction = new(_Connection.BeginTransactionAsync(token).Result);
      }
    }

    return Task.FromResult(ret);
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
    await _Connection.CloseAsync();
    await _Connection.DisposeAsync();
  }

  public void Dispose()
  {
    _Connection.Close();
    _Connection.Dispose();
  }
}
