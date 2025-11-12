namespace GT4.Core.Utils;

public interface ICancellationTokenProvider
{
  CancellationTokenHost CreateDbCancellationToken();
  CancellationTokenHost CreateShortOperationCancellationToken();
}
