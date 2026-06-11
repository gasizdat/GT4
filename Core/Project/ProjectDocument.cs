using GT4.Core.Project.Abstraction;
using Microsoft.Data.Sqlite;
using System.Data;

namespace GT4.Core.Project;

/// <summary>
/// Thread-safe abstraction over a single <see cref="SqliteConnection"/>.
/// <para>
/// A <see cref="SqliteConnection"/> cannot service more than one command at a time, so every
/// statement is serialized through <see cref="_Gate"/>. Commands may be created and executed from
/// any number of threads/async-flows; the gate guarantees they touch the connection one at a time.
/// </para>
/// <para>
/// Transactions are flow-affine. <see cref="BeginTransactionAsync"/> acquires the gate and keeps it
/// for the whole lifetime of the root transaction, recording the transaction in an
/// <see cref="AsyncLocal{T}"/> ambient. Statements issued by the owning flow observe the ambient and
/// run directly (the flow already owns the connection); statements from any other flow find no
/// ambient and block on the gate until the transaction completes. Consequently only one transaction
/// can be active at a time, and only the creating flow can use it. Nested transactions on the same
/// flow are mapped to SAVEPOINTs.
/// </para>
/// </summary>
internal sealed class ProjectDocument : IProjectDocument, IAsyncDisposable, IDisposable
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
  private readonly RelativesProvider _RelativesProvider;

  // Serializes access to the single connection. A root transaction holds it for its whole lifetime;
  // a standalone statement holds it only while it executes.
  private readonly SemaphoreSlim _Gate = new(1, 1);

  // The innermost transaction active on the current async-flow, or null when the flow runs no
  // transaction. Because it flows with the ExecutionContext it survives awaits and thread hops,
  // which makes it the correct "same thread" notion for async code.
  private readonly AsyncLocal<NestedTransaction?> _Ambient = new();

  private readonly object _RevisionLock = new();
  private long _TransactionNo = 0;
  private long _ProjectRevision = Environment.TickCount64;
  private volatile bool _Disposed = false;

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
    _RelativesProvider = new(this);
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

    // Created sequentially: every statement must take its turn on the single connection, and a
    // transaction is owned exclusively by its flow.
    await _TableMetadata.CreateAsync(token);
    await _TableNames.CreateAsync(token);
    await _TablePersons.CreateAsync(token);
    await _TablePersonNames.CreateAsync(token);
    await _TableData.CreateAsync(token);
    await _TableRelatives.CreateAsync(token);
    await _TablePersonData.CreateAsync(token);

    transaction.Commit();
  }

  public ITableMetadata Metadata => _TableMetadata;
  public ITableNames Names => _TableNames;
  public ITablePersons Persons => _TablePersons;
  public ITablePersonNames PersonNames => _TablePersonNames;
  public ITableData Data => _TableData;
  public ITableRelatives Relatives => _TableRelatives;
  public ITablePersonData PersonData => _TablePersonData;

  public IFamilyManager FamilyManager => _FamilyManager;
  public IPersonManager PersonManager => _PersonManager;
  public IRelativesProvider RelativesProvider => _RelativesProvider;
  public long ProjectRevision => Interlocked.Read(ref _ProjectRevision);

  public void UpdateRevision()
  {
    CheckForDisposed();
    lock (_RevisionLock)
    {
      _ProjectRevision = Environment.TickCount64;
    }
  }

  // --- Connection access used internally by NestedTransaction ------------------------------------

  internal SqliteConnection Connection => _Connection;

  internal long NextTransactionNo() => Interlocked.Increment(ref _TransactionNo);

  /// <summary>Pops the ambient back to the enclosing transaction and releases the gate for a root.</summary>
  internal void LeaveTransaction(NestedTransaction transaction)
  {
    _Ambient.Value = transaction.Parent;
    if (transaction.Parent is null)
    {
      // The root transaction releases the exclusive hold on the connection.
      _Gate.Release();
    }
  }

  // --- Command creation and gated execution ------------------------------------------------------

  public SqliteCommand CreateCommand()
  {
    CheckForDisposed();
    return _Connection.CreateCommand();
  }

  public Task<int> ExecuteNonQueryAsync(SqliteCommand command, CancellationToken token) =>
    RunGatedAsync(command, () => command.ExecuteNonQueryAsync(token), token);

  public Task<object?> ExecuteScalarAsync(SqliteCommand command, CancellationToken token) =>
    RunGatedAsync(command, () => command.ExecuteScalarAsync(token), token);

  public Task<TResult> ExecuteReaderAsync<TResult>(SqliteCommand command, Func<SqliteDataReader, Task<TResult>> readAsync, CancellationToken token) =>
    RunGatedAsync(command, async () =>
    {
      await using var reader = await command.ExecuteReaderAsync(token);
      return await readAsync(reader);
    }, token);

  /// <summary>
  /// Executes <paramref name="run"/> with exclusive access to the connection and the command bound to
  /// the correct transaction. When the flow owns a transaction the command joins it; otherwise the
  /// gate guarantees no transaction is active, so the command is detached from any (possibly stale)
  /// transaction that <see cref="SqliteConnection.CreateCommand"/> may have stamped at creation time.
  /// </summary>
  private async Task<T> RunGatedAsync<T>(SqliteCommand command, Func<Task<T>> run, CancellationToken token)
  {
    CheckForDisposed();
    var ambient = _Ambient.Value;
    if (ambient is not null)
    {
      command.Transaction = ambient.RootDbTransaction;
      return await run();
    }

    await _Gate.WaitAsync(token);
    try
    {
      command.Transaction = null;
      return await run();
    }
    finally
    {
      _Gate.Release();
    }
  }

  public async Task<int> GetLastInsertRowIdAsync(CancellationToken token)
  {
    CheckForDisposed();
    using var command = CreateCommand();
    command.CommandText = "SELECT last_insert_rowid();";
    return Convert.ToInt32(await ExecuteScalarAsync(command, token));
  }

  public Task<IDbTransaction> BeginTransactionAsync(CancellationToken token)
  {
    CheckForDisposed();

    // Implemented synchronously and returned as a completed task on purpose: setting the ambient
    // inside an async continuation would not be observed by the caller (ExecutionContext changes do
    // not flow back up). Doing the work before any await keeps the ambient visible to the caller.
    var current = _Ambient.Value;
    NestedTransaction transaction;

    if (current is null)
    {
      // Root transaction: take exclusive ownership of the connection for the whole lifetime.
      _Gate.Wait(token);
      try
      {
        var dbTransaction = _Connection.BeginTransaction();
        transaction = NestedTransaction.CreateRoot(this, dbTransaction);
      }
      catch
      {
        _Gate.Release();
        throw;
      }
    }
    else
    {
      // Nested transaction on the same flow: SAVEPOINT inside the already-owned connection.
      transaction = NestedTransaction.CreateSavepoint(this, current);
    }

    _Ambient.Value = transaction;
    return Task.FromResult<IDbTransaction>(transaction);
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
    if (_Disposed)
    {
      return;
    }
    _Disposed = true;
    GC.SuppressFinalize(this);
    await _Connection.CloseAsync();
    await _Connection.DisposeAsync();
    _Gate.Dispose();
  }

  public void Dispose()
  {
    if (_Disposed)
    {
      return;
    }
    _Disposed = true;
    GC.SuppressFinalize(this);
    _Connection.Close();
    _Connection.Dispose();
    _Gate.Dispose();
  }
}
