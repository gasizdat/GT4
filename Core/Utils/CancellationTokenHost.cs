using System.Diagnostics;

namespace GT4.Core.Utils;

public class CancellationTokenHost : IDisposable
{
  private readonly CancellationTokenSource _TokenSource;

  public CancellationTokenHost(TimeSpan timeout)
  {
    _TokenSource = Debugger.IsAttached ? new() : new (timeout);
  }

  public CancellationToken Token => _TokenSource.Token;
  public static implicit operator CancellationToken(CancellationTokenHost cancellationTokenHost) => cancellationTokenHost.Token;

  public void Dispose()
  {
    _TokenSource.Dispose();
  }
}
