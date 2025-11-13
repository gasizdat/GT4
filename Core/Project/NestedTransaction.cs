using System.Data;

namespace GT4.Core.Project;

// TODO Add count for nested transactions 
// TODO Add count for nested commited transactions 
// TODO Check if some nested transaction was not commited, than - rollback

public class NestedTransaction : IDisposable, IAsyncDisposable, IDbTransaction
{
  private readonly string _TransactionName;
  private readonly IDbTransaction? _DbTransaction;
  private readonly ProjectDocument? _Document;
  private readonly NestedTransaction? _ParentTransaction;
  private bool _Disposed = false;
  private bool _Commited = false;
  private bool _Reverted = false;

  private IDbTransaction _InnerTransaction => _DbTransaction ?? _ParentTransaction!._InnerTransaction;

  public NestedTransaction(IDbTransaction dbTransaction, ProjectDocument document)
  {
    _TransactionName = "Initial";
    _DbTransaction = dbTransaction;
    _Document = document;
  }

  ~NestedTransaction()
  {
    if (!_Disposed)
      throw new ApplicationException($"The transaction {_TransactionName} has leaked");

    if (!_Commited && !_Reverted)
      throw new ApplicationException($"The transaction {_TransactionName} is hanged");
  }

  public NestedTransaction(NestedTransaction parentTransaction, string innerTransactionName)
  {
    _ParentTransaction = parentTransaction;
    _TransactionName = innerTransactionName;
    if (Connection is null)
      return;

    var command = Connection.CreateCommand();
    command.CommandText = $"SAVEPOINT {innerTransactionName};";
    command.ExecuteNonQuery();
  }

  public IDbConnection? Connection => _InnerTransaction.Connection;

  public IsolationLevel IsolationLevel => _InnerTransaction.IsolationLevel;

  public bool IsDisposed => _Disposed;

  public void Commit()
  {
    if (_Commited || _Reverted || _Disposed)
      throw new ApplicationException($"The transaction {_TransactionName} is not in the correct state");

    _Commited = true;

    if (_DbTransaction is not null)
    {
      _DbTransaction.Commit();
      _Document?.UpdateRevision();
    }
    else if (Connection is not null)
    {
      var command = Connection.CreateCommand();
      command.CommandText = $"RELEASE SAVEPOINT {_TransactionName};";
      command.ExecuteNonQuery();
    }
  }

  public void Rollback()
  {
    if (_Commited || _Disposed)
      throw new ApplicationException("The transaction is not in the correct state");
    
    if (_Reverted)
      return;

    _Reverted = true;

    if (_DbTransaction is not null)
    {
      _DbTransaction.Rollback();
    }
    else if (Connection is not null)
    {
      var command = Connection.CreateCommand();
      command.CommandText = $"ROLLBACK TO SAVEPOINT {_TransactionName};";
      command.ExecuteNonQuery();
    }
  }

  public void Dispose()
  {
    if (_Disposed)
      return;

    _Disposed = true;

    if (_DbTransaction is not null)
    {
      _DbTransaction.Dispose();
    }
    else if (!_Commited)
    {
      Rollback();
    }
  }

  public ValueTask DisposeAsync()
  {
    return new ValueTask(new Task(Dispose));
  }
}
