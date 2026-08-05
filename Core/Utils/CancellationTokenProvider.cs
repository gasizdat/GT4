namespace GT4.Core.Utils;

internal class CancellationTokenProvider : ICancellationTokenProvider
{
  public CancellationTokenHost CreateDbCancellationToken() => new CancellationTokenHost(TimeSpan.FromSeconds(5));

  public CancellationTokenHost CreateShortOperationCancellationToken() => new CancellationTokenHost(TimeSpan.FromSeconds(10));
}
