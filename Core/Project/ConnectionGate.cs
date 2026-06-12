namespace GT4.Core.Project;

/// <summary>
/// Shared synchronization for the single SQLite connection: a 1-permit semaphore that serializes
/// access plus the ambient transaction of the current async-flow. One instance is owned by a
/// <see cref="ProjectDocument"/> and shared with every <see cref="ProjectCommand"/> and
/// <see cref="NestedTransaction"/> it creates.
/// <para>
/// Closing is a drain: <see cref="Close"/>/<see cref="CloseAsync"/> mark the gate closed and then
/// acquire it, which waits out the in-flight statement, open reader or active transaction. The
/// document releases the gate again after the connection is shut so queued waiters wake up, observe
/// <see cref="IsClosed"/> and fail with <see cref="ObjectDisposedException"/> instead of hanging or
/// touching a closed connection. The semaphore itself is never disposed — disposing a
/// <see cref="SemaphoreSlim"/> does not wake its waiters, and one without
/// <see cref="SemaphoreSlim.AvailableWaitHandle"/> holds no unmanaged resources anyway.
/// </para>
/// </summary>
internal sealed class ConnectionGate
{
  private readonly SemaphoreSlim _Semaphore = new(1, 1);
  private volatile bool _Closed;

  // The innermost transaction active on the current async-flow, or null when the flow runs no
  // transaction. Because it flows with the ExecutionContext it survives awaits and thread hops,
  // which makes it the correct "same thread" notion for async code.
  private readonly AsyncLocal<NestedTransaction?> _Ambient = new();

  internal NestedTransaction? Current
  {
    get => _Ambient.Value;
    set => _Ambient.Value = value;
  }

  internal bool IsClosed => _Closed;

  /// <summary>Throws when the gate is closed. Call after a successful wait, before using the connection.</summary>
  internal void ThrowIfClosed()
  {
    if (_Closed)
    {
      throw new ObjectDisposedException(nameof(ProjectDocument));
    }
  }

  internal void Wait(CancellationToken token) => _Semaphore.Wait(token);

  internal Task WaitAsync(CancellationToken token) => _Semaphore.WaitAsync(token);

  internal void Release() => _Semaphore.Release();

  /// <summary>Marks the gate closed and waits until the in-flight operation (if any) completes.</summary>
  internal void Close()
  {
    _Closed = true;
    _Semaphore.Wait();
  }

  /// <inheritdoc cref="Close"/>
  internal Task CloseAsync()
  {
    _Closed = true;
    return _Semaphore.WaitAsync(CancellationToken.None);
  }
}
