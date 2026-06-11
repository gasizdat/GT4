namespace GT4.Core.Project;

/// <summary>
/// Shared synchronization for the single SQLite connection: a 1-permit semaphore that serializes
/// access plus the ambient transaction of the current async-flow. One instance is owned by a
/// <see cref="ProjectDocument"/> and shared with every <see cref="ProjectCommand"/> and
/// <see cref="NestedTransaction"/> it creates.
/// </summary>
internal sealed class ConnectionGate : IDisposable
{
  private readonly SemaphoreSlim _Semaphore = new(1, 1);

  // The innermost transaction active on the current async-flow, or null when the flow runs no
  // transaction. Because it flows with the ExecutionContext it survives awaits and thread hops,
  // which makes it the correct "same thread" notion for async code.
  private readonly AsyncLocal<NestedTransaction?> _Ambient = new();

  internal NestedTransaction? Current
  {
    get => _Ambient.Value;
    set => _Ambient.Value = value;
  }

  internal void Wait(CancellationToken token) => _Semaphore.Wait(token);

  internal Task WaitAsync(CancellationToken token) => _Semaphore.WaitAsync(token);

  internal void Release() => _Semaphore.Release();

  public void Dispose() => _Semaphore.Dispose();
}
