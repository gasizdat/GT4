using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GT4.Core.Utils;

public class DefaultCancellationToken : IDisposable
{
  private readonly CancellationTokenSource _TokenSource = new (TimeSpan.FromSeconds(5));

  public CancellationToken Token => _TokenSource.Token;
  public static implicit operator CancellationToken(DefaultCancellationToken defaultCancellationToken) => defaultCancellationToken.Token;

  public void Dispose()
  {
    _TokenSource.Dispose();
  }
}
