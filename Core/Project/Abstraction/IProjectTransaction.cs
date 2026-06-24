namespace GT4.Core.Project.Abstraction;

/// <summary>
/// A transaction handle bound to the async-flow that created it. The root transaction owns the
/// document's single connection for its whole lifetime; transactions started while one is already
/// active on the same flow are nested as SQLite SAVEPOINTs.
/// <para>
/// All members are intended to be used only from the flow that created the transaction. Commit is
/// asynchronous because committing the root transaction also persists the project revision, which is
/// an async database write; a synchronous commit would have to block on it.
/// </para>
/// </summary>
public interface IProjectTransaction : IDisposable, IAsyncDisposable
{
  /// <summary>
  /// Commits this transaction. For the root this persists the project revision and commits the
  /// underlying SQLite transaction; for a nested transaction it releases its SAVEPOINT. The handle's
  /// hold on the connection (gate and ambient) is released when it is disposed.
  /// </summary>
  Task CommitAsync(CancellationToken token);

  /// <summary>Rolls the transaction back. A handle disposed without a commit rolls back automatically.</summary>
  void Rollback();
}
