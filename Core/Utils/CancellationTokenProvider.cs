namespace GT4.Core.Utils;

internal class CancellationTokenProvider : ICancellationTokenProvider
{
  public CancellationTokenHost CreateDbCancellationToken()
  {
    return new CancellationTokenHost(TimeSpan.FromSeconds(5));
  }

  public CancellationTokenHost CreateShortOperationCancellationToken()
  {
    return new CancellationTokenHost(TimeSpan.FromSeconds(10));
  }
}
