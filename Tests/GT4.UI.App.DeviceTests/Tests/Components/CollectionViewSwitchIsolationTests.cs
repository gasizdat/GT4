using System.Diagnostics;
using Xunit;

namespace GT4.UI.DeviceTests;

// Isolation probe for MemoryGrowthReproTests: that test showed managed heap compounding across
// repeated CollectionView.ItemsSource swaps between a large and a tiny array, even though both
// arrays are the exact same instances every cycle. This strips out CardView, SafeBindableLayout and
// PersonInfoView entirely -- a bare CollectionView with a one-Label-per-cell template -- to see
// whether the compounding survives. If it does, the leak is in CollectionView's own
// ItemsSource-reassignment cell teardown, not in anything GT4-specific.
public class CollectionViewSwitchIsolationTests
{
  [Fact]
  public async Task Repeated_ItemsSource_swaps_between_a_large_and_tiny_array_do_not_compound()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var large = Enumerable.Range(0, 5000).Select(i => $"Item{i}").ToArray();
    var small = new[] { "A", "B", "C" };

    var template = new DataTemplate(() =>
    {
      var label = new Label();
      label.SetBinding(Label.TextProperty, new Binding("."));
      return label;
    });

    var collectionView = new CollectionView { ItemsSource = large, ItemTemplate = template, HeightRequest = 600 };
    var page = new ContentPage { Content = collectionView };
    await using var window = await WindowHost.AttachAsync(page);
    await Task.Delay(500);

    (long WorkingSet, long Managed) Sample()
    {
      GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
      GC.WaitForPendingFinalizers();
      GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
      return (Process.GetCurrentProcess().WorkingSet64, GC.GetTotalMemory(forceFullCollection: false));
    }

    var samples = new List<(long WorkingSet, long Managed)> { Sample() };

    for (var cycle = 0; cycle < 5; cycle++)
    {
      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = small);
      await Task.Delay(300);
      samples.Add(Sample());

      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = large);
      await Task.Delay(300);
      samples.Add(Sample());
    }

    var report = $"pid={Environment.ProcessId} samples(WS/managed MB)=" +
      $"[{string.Join(", ", samples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}]";

    // TEMP diagnostic: force a failure to see the sample report.
    Assert.Fail(report);
  }
}
