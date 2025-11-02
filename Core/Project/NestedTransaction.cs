using System.Data;

namespace GT4.Core.Project;

// TODO Add count for nested transactions 
// TODO Add count for nested commited transactions 
// TODO Check if some nested transaction was not commited, than - rollback

public class NestedTransaction : IDisposable, IAsyncDisposable, IDbTransaction
{
  private readonly IDbTransaction? _DbTransaction;
  private readonly NestedTransaction? _ParentTransaction;
  private bool _Disposed = false;
  private bool _Commited = false;

  private IDbTransaction _InnerTransaction => _DbTransaction ?? _ParentTransaction!._InnerTransaction;

  public NestedTransaction(IDbTransaction dbTransaction)
  {
    _DbTransaction = dbTransaction;
  }

  public NestedTransaction(NestedTransaction parentTransaction)
  {
    _ParentTransaction = parentTransaction;
  }

  public IDbConnection? Connection => _InnerTransaction.Connection;

  public IsolationLevel IsolationLevel => _InnerTransaction.IsolationLevel;

  public bool IsDisposed => _Disposed;

  public void Commit()
  {
    _Commited = true;
    if (_DbTransaction is not null)
      _DbTransaction.Commit();
  }

  public void Rollback()
  {
    _InnerTransaction.Rollback();
  }

  public void Dispose()
  {
    lock (this)
    {
      if (_Disposed)
      {
        return;
      }
      _Disposed = true;
    }

    if (_DbTransaction is not null)
    {
      _DbTransaction.Dispose();
    }
    else if (!_Commited)
    {
      _InnerTransaction.Dispose();
    }
  }

  public ValueTask DisposeAsync()
  {
    return new ValueTask(new Task(Dispose));
  }

}
