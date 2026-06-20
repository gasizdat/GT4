using Microsoft.Data.Sqlite;
using System.Data;

namespace GT4.Core.Project;

/// <summary>
/// A transaction handle bound to the async-flow that created it.
/// <para>
/// The first (root) transaction on a flow takes a real <see cref="SqliteTransaction"/> and owns
/// the document's connection gate for its whole lifetime, so no other flow can touch the single
/// connection until it completes. Transactions started while one is already active on the same flow
/// are implemented as SQLite SAVEPOINTs nested inside the root.
/// </para>
/// <para>
/// All members are intended to be used only from the flow that created the transaction (the same
/// rule SQLite imposes). Other flows never observe this instance through the document's ambient, so
/// they cannot use it; they simply block on the gate until the root completes.
/// </para>
/// </summary>
internal sealed class NestedTransaction : IDbTransaction, IDisposable, IAsyncDisposable
{
  private readonly ProjectDocument _Document;
  private readonly NestedTransaction? _Parent;          // null for the root transaction
  private readonly SqliteTransaction? _DbTransaction;   // non-null only for the root transaction
  private readonly string? _SavepointName;              // non-null only for nested transactions
  private readonly string _Name;

#if DEBUG
  private readonly System.Diagnostics.StackTrace _StackTrace = new(fNeedFileInfo: true);
#endif

  private bool _Committed;
  private bool _RolledBack;
  private bool _Completed;
  private bool _Disposed;

  private NestedTransaction(ProjectDocument document, SqliteTransaction dbTransaction)
  {
    _Document = document;
    _DbTransaction = dbTransaction;
    _Parent = null;
    _Name = "Root";
  }

  private NestedTransaction(ProjectDocument document, NestedTransaction parent, string savepointName)
  {
    _Document = document;
    _Parent = parent;
    _SavepointName = savepointName;
    _Name = savepointName;
  }

  /// <summary>Creates the root transaction. The caller must already hold the connection gate.</summary>
  internal static NestedTransaction CreateRoot(ProjectDocument document, SqliteTransaction dbTransaction) =>
    new(document, dbTransaction);

  /// <summary>Creates a nested transaction (SAVEPOINT) inside <paramref name="parent"/> on the same flow.</summary>
  internal static NestedTransaction CreateSavepoint(ProjectDocument document, NestedTransaction parent)
  {
    var name = $"SP_{document.NextTransactionNo()}";
    ExecuteSimple(document, $"SAVEPOINT {name};");
    return new(document, parent, name);
  }

  private bool IsRoot => _DbTransaction is not null;

  internal NestedTransaction? Parent => _Parent;

  /// <summary>The real SQLite transaction at the root of this (possibly nested) transaction.</summary>
  internal SqliteTransaction RootDbTransaction => _DbTransaction ?? _Parent!.RootDbTransaction;

  public IDbConnection? Connection => _Document.Connection;

  public IsolationLevel IsolationLevel => IsolationLevel.Serializable;

  public bool IsDisposed => _Disposed;

  private static void ExecuteSimple(ProjectDocument document, string sql)
  {
    using var command = document.Connection.CreateCommand();
    command.CommandText = sql;
    command.ExecuteNonQuery();
  }

  public void Commit()
  {
    if (_Committed || _RolledBack || _Disposed)
    {
      throw new InvalidOperationException($"The transaction '{_Name}' is not in a committable state.");
    }

    try
    {
      if (IsRoot)
      {
        // TODO: This is not a very good solution for updating the project revision, as we need
        // to create a CancellationToken directly and then wait for the operation synchronously.
        _Document
          .Metadata
          .SetProjectRevisionAsync(DateTime.Now.ToString(), CancellationToken.None)
          .Wait();
        _DbTransaction!.Commit();
        _Document.UpdateRevision();
        _Committed = true;
      }
      else
      {
        ExecuteSimple(_Document, $"RELEASE SAVEPOINT {_SavepointName};");
        _Committed = true;
      }
    }
    finally
    {
      Complete();
    }
  }

  public void Rollback()
  {
    if (_Committed || _Disposed)
    {
      throw new InvalidOperationException($"The transaction '{_Name}' is not in a rollback-able state.");
    }

    if (_RolledBack)
    {
      return;
    }

    _RolledBack = true;

    try
    {
      if (IsRoot)
      {
        _DbTransaction!.Rollback();
      }
      else
      {
        ExecuteSimple(_Document, $"ROLLBACK TO SAVEPOINT {_SavepointName};");
      }
    }
    finally
    {
      Complete();
    }
  }

  public void Dispose()
  {
    if (_Disposed)
    {
      return;
    }

    // Roll back before flagging disposal: an uncommitted transaction is undone when its scope exits.
    if (!_Committed && !_RolledBack)
    {
      Rollback();
    }

    _Disposed = true;
    Complete();
  }

  public ValueTask DisposeAsync()
  {
    Dispose();
    return ValueTask.CompletedTask;
  }

  /// <summary>
  /// Releases the transaction's hold on the flow exactly once: pops the ambient back to the enclosing
  /// transaction and, for the root, releases the connection gate and disposes the real transaction.
  /// Runs synchronously so the ambient change is observed by the caller that owns the scope.
  /// </summary>
  private void Complete()
  {
    if (_Completed)
    {
      return;
    }

    _Completed = true;
    _Document.LeaveTransaction(this);

    if (IsRoot)
    {
      _DbTransaction!.Dispose();
    }
  }

#if DEBUG
  ~NestedTransaction()
  {
    if (!_Disposed)
    {
      System.Diagnostics.Debug.WriteLine($"The transaction '{_Name}' leaked without being disposed.\n{_StackTrace}");
    }
  }
#endif
}
