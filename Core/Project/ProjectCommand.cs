using Microsoft.Data.Sqlite;

namespace GT4.Core.Project;

/// <summary>
/// A SQLite command that executes itself safely against the document's single connection.
/// <para>
/// Created by <see cref="Abstraction.IProjectDocument.CreateCommand"/>. Set <see cref="CommandText"/>
/// and <see cref="Parameters"/> as on a normal command, then call one of the <c>Execute*</c> methods.
/// Execution is serialized through the shared <see cref="ConnectionGate"/>: when the calling
/// async-flow owns the current transaction the command joins it and runs directly (the flow already
/// holds the connection); otherwise it waits for exclusive access. The command is also (re)bound to
/// the correct transaction at execution time, so it never uses a stale one that
/// <see cref="SqliteConnection.CreateCommand"/> may have stamped at creation time.
/// </para>
/// </summary>
public sealed class ProjectCommand : IDisposable, IAsyncDisposable
{
  private readonly SqliteCommand _Command;
  private readonly ConnectionGate _Gate;
  private readonly object _CommandLock;

  internal ProjectCommand(SqliteCommand command, ConnectionGate gate, object commandLock)
  {
    _Command = command;
    _Gate = gate;
    _CommandLock = commandLock;
  }

  public string CommandText
  {
    get => _Command.CommandText;
    set => _Command.CommandText = value;
  }

  public SqliteParameterCollection Parameters => _Command.Parameters;

  public Task<int> ExecuteNonQueryAsync(CancellationToken token) =>
    RunGatedAsync(() => _Command.ExecuteNonQueryAsync(token), token);

  public Task<object?> ExecuteScalarAsync(CancellationToken token) =>
    RunGatedAsync(() => _Command.ExecuteScalarAsync(token), token);

  /// <summary>
  /// Executes a reader. The returned <see cref="ProjectDataReader"/> is used like any reader and holds
  /// the connection until it is disposed, so always consume it with <c>await using</c>. While it is
  /// open no other flow can touch the connection, so do not start an independent database operation
  /// before disposing it (that would deadlock); read what you need, dispose, then continue.
  /// </summary>
  public async Task<ProjectDataReader> ExecuteReaderAsync(CancellationToken token)
  {
    var ambient = _Gate.Current;
    if (ambient is not null)
    {
      // This flow owns the transaction (and therefore the connection); the transaction holds the gate.
      _Command.Transaction = ambient.RootDbTransaction;
      var reader = await _Command.ExecuteReaderAsync(token);
      return new ProjectDataReader(reader, gate: null);
    }

    await _Gate.WaitAsync(token);
    try
    {
      // The document may have been disposed while this flow was queued on the gate.
      _Gate.ThrowIfClosed();
      // Holding the gate guarantees no transaction is active, so detach from any stale transaction.
      _Command.Transaction = null;
      var reader = await _Command.ExecuteReaderAsync(token);
      // The gate is released when the returned reader is disposed.
      return new ProjectDataReader(reader, _Gate);
    }
    catch
    {
      _Gate.Release();
      throw;
    }
  }

  /// <summary>
  /// Synchronous counterpart to <see cref="ExecuteNonQueryAsync"/>, for the rare case where the work
  /// must finish on the caller's own flow rather than a continuation — e.g. stamping the revision while
  /// committing a transaction. The caller must already own the active transaction; this rebinds to it
  /// like the async path (so it never uses a stale stamped transaction) but does not take the gate.
  /// </summary>
  internal int ExecuteNonQuery()
  {
    _Command.Transaction = _Gate.Current?.RootDbTransaction;
    return _Command.ExecuteNonQuery();
  }

  private async Task<T> RunGatedAsync<T>(Func<Task<T>> run, CancellationToken token)
  {
    var ambient = _Gate.Current;
    if (ambient is not null)
    {
      // This flow owns the transaction (and therefore the connection); run directly.
      _Command.Transaction = ambient.RootDbTransaction;
      return await run();
    }

    await _Gate.WaitAsync(token);
    try
    {
      // The document may have been disposed while this flow was queued on the gate.
      _Gate.ThrowIfClosed();
      // Holding the gate guarantees no transaction is active, so detach from any stale transaction.
      _Command.Transaction = null;
      return await run();
    }
    finally
    {
      _Gate.Release();
    }
  }

  public void Dispose() { lock (_CommandLock) { _Command.Dispose(); } }

  public ValueTask DisposeAsync() { lock (_CommandLock) { _Command.Dispose(); } return ValueTask.CompletedTask; }
}
