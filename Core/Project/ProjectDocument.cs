using GT4.Core.Project.Abstraction;
using Microsoft.Data.Sqlite;
using System.Data;

namespace GT4.Core.Project;

internal class ProjectDocument : IProjectDocument, IAsyncDisposable, IDisposable
{
  private readonly SqliteConnection _Connection;
  private readonly TableMetadata _TableMetadata;
  private readonly TableNames _TableNames;
  private readonly TablePersons _TablePersons;
  private readonly TablePersonNames _TablePersonNames;
  private readonly TableData _TableData;
  private readonly TableRelatives _TableRelatives;
  private readonly TablePersonData _TablePersonData;
  private readonly FamilyManager _FamilyManager;
  private readonly PersonManager _PersonManager;

  private NestedTransaction? _CurrentTransaction = null;
  private long _TransactionNo = 0;
  private long _ProjectRevision = Environment.TickCount64;
  private bool _Disposed = false;

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
    _TableMetadata = new(this);
    _TableNames = new(this);
    _TablePersons = new(this);
    _TablePersonNames = new(this);
    _TableData = new(this);
    _TableRelatives = new(this);
    _TablePersonData = new(this);
    _FamilyManager = new(this);
    _PersonManager = new(this);
  }

  private void CheckForDisposed()
  {
    if (_Disposed)
    {
      throw new ObjectDisposedException(nameof(ProjectDocument));
    }
  }

  ~ProjectDocument()
  {
    Dispose();
  }

  private async Task OpenAsync(CancellationToken token)
  {
    CheckForDisposed();
    await _Connection.OpenAsync(token);
  }

  private async Task InitNewDBAsync(CancellationToken token)
  {
    CheckForDisposed();
    using var transaction = await BeginTransactionAsync(token);

    await Task.WhenAll(
      _TableMetadata.CreateAsync(token),
      _TableNames.CreateAsync(token),
      _TablePersons.CreateAsync(token),
      _TablePersonNames.CreateAsync(token),
      _TableData.CreateAsync(token),
      _TableRelatives.CreateAsync(token),
      _TablePersonData.CreateAsync(token)
    );

    transaction.Commit();
  }

  public const string MimeType = "application/gt4;storage=sqlite";

  public ITableMetadata Metadata => _TableMetadata;
  public ITableNames Names => _TableNames;
  public ITablePersons Persons => _TablePersons;
  public ITablePersonNames PersonNames => _TablePersonNames;
  public ITableData Data => _TableData;
  public ITableRelatives Relatives => _TableRelatives;
  public ITablePersonData PersonData => _TablePersonData;

  public IFamilyManager FamilyManager => _FamilyManager;
  public IPersonManager PersonManager => _PersonManager;
  public long ProjectRevision => _ProjectRevision;

  public void UpdateRevision()
  {
    CheckForDisposed();
    lock (this)
    {
      _ProjectRevision = Environment.TickCount64;
    }
  }

  public async Task<int> GetLastInsertRowIdAsync(CancellationToken token)
  {
    CheckForDisposed();
    using var command = CreateCommand();
    command.CommandText = "SELECT last_insert_rowid();";
    return Convert.ToInt32(await command.ExecuteScalarAsync(token));
  }

  public SqliteCommand CreateCommand()
  {
    CheckForDisposed();
    return _Connection.CreateCommand();
  }

  public Task<IDbTransaction> BeginTransactionAsync(CancellationToken token)
  {
    CheckForDisposed();
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
        ret = _CurrentTransaction = new NestedTransaction(_Connection.BeginTransactionAsync(token).Result, this);
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
    _Disposed = true;
    await _Connection.CloseAsync();
    await _Connection.DisposeAsync();
  }

  public void Dispose()
  {
    _Disposed = true;
    _Connection.Close();
    _Connection.Dispose();
  }
}
