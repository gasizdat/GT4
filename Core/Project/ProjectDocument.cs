using GT4.Core.Project.Abstraction;
using Microsoft.Data.Sqlite;
using System.Globalization;

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
  private readonly FamilyTreeProvider _FamilyTreeProvider;

  // Serializes access to the single connection (a root transaction holds it for its whole lifetime;
  // a standalone statement holds it only while it executes) and tracks the current flow's transaction.
  // Shared with every ProjectCommand and NestedTransaction created by this document.
  private readonly ConnectionGate _Gate = new();

  // Serializes SqliteConnection.AddCommand / RemoveCommand, which modify a non-thread-safe
  // List<WeakReference<SqliteCommand>> inside SqliteConnection. Shared with every ProjectCommand
  // so that CreateCommand and Dispose cannot race even when commands are created or disposed
  // concurrently from multiple async flows.
  private readonly object _CommandLock = new();

  private long _TransactionNo = 0;
  private long _ProjectRevision = Environment.TickCount64;
  private volatile bool _Disposed = false;
  private int _DisposeStarted = 0;

  static ProjectDocument()
  {
    SQLitePCL.Batteries.Init();
  }

  private ProjectDocument(string dbFileName, SqliteOpenMode mode)
  {
    var builder = new SqliteConnectionStringBuilder();
    builder.Pooling = false;
    builder.DataSource = dbFileName;
    builder.Mode = mode;
    var connectionString = builder.ConnectionString;
    _Connection = new SqliteConnection(connectionString);
    _TableMetadata = new(this);
    _TableNames = new(this);
    _TablePersons = new(this);
    _TablePersonNames = new(this);
    _TableData = new(this);
    _TableRelatives = new(this);
    _TablePersonData = new(this, _TableData);
    _FamilyManager = new(this);
    _PersonManager = new(this);
    _RelativesProvider = new(this);
    _FamilyTreeProvider = new(this);
  }

  private void CheckForDisposed()
  {
    if (_Disposed)
    {
      throw new ObjectDisposedException(nameof(ProjectDocument));
    }
  }

  private async Task OpenAsync(CancellationToken token)
  {
    CheckForDisposed();
    await _Connection.OpenAsync(token);

    // Foreign keys are off by default in SQLite and the setting is per-connection, so it must be
    // enabled right after opening. Without it the schema's FOREIGN KEY / ON DELETE CASCADE clauses
    // are ignored and removing a person or name would leave orphaned dependent rows.
    using var pragma = _Connection.CreateCommand();
    pragma.CommandText = "PRAGMA foreign_keys = ON;";
    await pragma.ExecuteNonQueryAsync(token);
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

    await transaction.CommitAsync(token);
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
  public IFamilyTreeProvider FamilyTreeProvider => _FamilyTreeProvider;
  public long ProjectRevision => Interlocked.Read(ref _ProjectRevision);

  public void UpdateRevision()
  {
    // Deliberately no disposed check: a transaction committing while a dispose drains the gate must
    // still stamp the revision, so the host flushes the cache back to the origin.
    Interlocked.Increment(ref _ProjectRevision);
  }

  internal SqliteConnection Connection => _Connection;

  internal long NextTransactionNo() => Interlocked.Increment(ref _TransactionNo);

  /// <summary>Pops the ambient back to the enclosing transaction and releases the gate for a root.</summary>
  internal void LeaveTransaction(NestedTransaction transaction)
  {
    _Gate.Current = transaction.Parent;
    if (transaction.Parent is null)
    {
      // The root transaction releases the exclusive hold on the connection.
      _Gate.Release();
    }
  }

  public ProjectCommand CreateCommand()
  {
    // A flow that owns the active transaction may keep issuing statements while a dispose is
    // draining the gate (the transaction holds the connection until it completes); only new
    // standalone work is rejected.
    if (_Gate.Current is null)
    {
      CheckForDisposed();
    }
    SqliteCommand cmd;
    lock (_CommandLock) { cmd = _Connection.CreateCommand(); }
    return new ProjectCommand(cmd, _Gate, _CommandLock);
  }

  public Task<IProjectTransaction> BeginTransactionAsync(CancellationToken token)
  {
    // Implemented synchronously and returned as a completed task on purpose: setting the ambient
    // inside an async continuation would not be observed by the caller (ExecutionContext changes do
    // not flow back up). Doing the work before any await keeps the ambient visible to the caller.
    var current = _Gate.Current;
    NestedTransaction transaction;

    if (current is null)
    {
      // Only new root transactions are rejected after dispose; a flow that owns the active
      // transaction may still nest savepoints while a dispose drains the gate.
      CheckForDisposed();
      // Root transaction: take exclusive ownership of the connection for the whole lifetime.
      _Gate.Wait(token);
      try
      {
        // The document may have been disposed while this flow was queued on the gate.
        _Gate.ThrowIfClosed();
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

    _Gate.Current = transaction;
    return Task.FromResult<IProjectTransaction>(transaction);
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

  private void ThrowIfDisposingInsideTransaction()
  {
    if (_Gate.Current is not null)
    {
      throw new InvalidOperationException(
        "Cannot dispose the document from a flow that owns an active transaction.");
    }
  }

  public async ValueTask DisposeAsync()
  {
    ThrowIfDisposingInsideTransaction();
    if (Interlocked.Exchange(ref _DisposeStarted, 1) != 0)
    {
      return;
    }
    _Disposed = true;
    await _Gate.CloseAsync();
    await _Connection.CloseAsync();
    await _Connection.DisposeAsync();
    _Gate.Release();
  }

  public void Dispose()
  {
    ThrowIfDisposingInsideTransaction();
    if (Interlocked.Exchange(ref _DisposeStarted, 1) != 0)
    {
      return;
    }
    _Disposed = true;
    _Gate.Close();
    _Connection.Close();
    _Connection.Dispose();
    _Gate.Release();
  }
}
