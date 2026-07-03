namespace GT4.UI.DeviceTests;

/// <summary>
/// Polls until a condition holds. Use this instead of subscribing to a single PropertyChanged event
/// when the triggering reload may already be in flight (or already complete) by the time the caller
/// starts watching -- e.g. NamesPage's CollectionView is live-bound to Names via x:Reference, so
/// RequestUpdateNames's OnPropertyChanged(nameof(Names)) can drive an automatic reload on its own
/// schedule, racing ahead of a listener subscribed after the fact.
/// </summary>
internal static class Poll
{
  public static async Task<T> UntilAsync<T>(Func<Task<T>> probe, Func<T, bool> isReady, TimeSpan? timeout = null)
  {
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));

    while (true)
    {
      var value = await probe();
      if (isReady(value))
      {
        return value;
      }

      if (DateTime.UtcNow >= deadline)
      {
        throw new TimeoutException("Condition was not met before the timeout.");
      }

      await Task.Delay(20);
    }
  }
}
