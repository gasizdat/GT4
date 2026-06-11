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

  internal ProjectCommand(SqliteCommand command, ConnectionGate gate)
  {
    _Command = command;
    _Gate = gate;
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
  /// Executes a reader and projects it via <paramref name="readAsync"/> while the connection is held
  /// exclusively. The callback must only read from the reader; it must not start other database
  /// operations (that would deadlock against the held connection).
  /// </summary>
  public Task<TResult> ExecuteReaderAsync<TResult>(Func<SqliteDataReader, Task<TResult>> readAsync, CancellationToken token) =>
    RunGatedAsync(async () =>
    {
      await using var reader = await _Command.ExecuteReaderAsync(token);
      return await readAsync(reader);
    }, token);

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
      // Holding the gate guarantees no transaction is active, so detach from any stale transaction.
      _Command.Transaction = null;
      return await run();
    }
    finally
    {
      _Gate.Release();
    }
  }

  public void Dispose() => _Command.Dispose();

  public ValueTask DisposeAsync() => _Command.DisposeAsync();
}
