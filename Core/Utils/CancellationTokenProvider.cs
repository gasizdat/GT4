namespace GT4.Core.Utils;

internal class CancellationTokenProvider : ICancellationTokenProvider
{
  public CancellationTokenHost CreateDbCancellationToken()
  {
    return new CancellationTokenHost();
  }
}
