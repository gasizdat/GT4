using System.Diagnostics;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

// Diagnostic reproduction for a reported unbounded memory growth while scrolling ProjectPage's
// family-card list on a large project with no user photos -- every card renders only the cached
// default stub image. Mirrors ProjectPage.xaml's real CardView/SafeBindableLayout/PersonInfoView
// visual tree against synthetic data at a scale representative of a large real project, and tracks
// post-GC working set across many scroll-driven recycle cycles instead of asserting collectibility:
// a leaked native decode or platform handler need not leak any managed object, so
// PersonInfoViewLeakTests' WeakReference approach cannot see it. Set GT4_GCDUMP_PAUSE=1 to pause for
// 20s at two checkpoints (early/late) so `dotnet-gcdump collect -p <pid>` can attach externally.
public class MemoryGrowthReproTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex) =>
    new(id, UnknownDate, null, sex, [N(id * 100, firstName, NameType.FirstName)], null);

  private const int FamilyCount = 3000;
  private const int PersonsPerFamily = 8;
  private const int ScrollRounds = 80;

  // Hard safety ceiling: abort the loop rather than let working set climb toward the multi-GB
  // territory the user is trying to avoid on their own machine.
  private const long WorkingSetAbortBytes = 1_500_000_000;

  [Fact]
  public async Task Scrolling_a_large_family_list_does_not_grow_working_set_unbounded()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var families = Enumerable.Range(0, FamilyCount)
      .Select(i => new FamilyInfoItem(
        N(i, $"Family{i}", NameType.FamilyName),
        Enumerable.Range(0, PersonsPerFamily)
          .Select(j => P(i * 1000 + j, $"P{i}_{j}", j % 2 == 0 ? BiologicalSex.Male : BiologicalSex.Female))
          .ToArray(),
        (_, _) => true))
      .ToArray();

    var template = new DataTemplate(() =>
    {
      var layout = new SafeBindableLayout
      {
        ItemTemplate = new DataTemplate(() =>
        {
          var view = new PersonInfoView();
          view.SetBinding(PersonInfoView.PersonProperty, new Binding("."));
          return view;
        })
      };
      layout.SetBinding(SafeBindableLayout.ItemsSourceProperty, new Binding(nameof(FamilyInfoItem.DisplayedPersons)));

      var label = new Label();
      label.SetBinding(Label.TextProperty, new Binding($"{nameof(FamilyInfoItem.Info)}.{nameof(Name.Value)}"));

      return new CardView { Content = new VerticalStackLayout { Children = { label, layout } } };
    });

    var collectionView = new CollectionView
    {
      ItemsSource = families,
      ItemTemplate = template,
      HeightRequest = 600,
    };

    var page = new ContentPage { Content = collectionView };
    await using var window = await WindowHost.AttachAsync(page);

    await Task.Delay(500);

    var pid = Environment.ProcessId;
    var pauseForDump = Environment.GetEnvironmentVariable("GT4_GCDUMP_PAUSE") == "1";

    (long WorkingSet, long Managed) Sample()
    {
      GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
      GC.WaitForPendingFinalizers();
      GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
      return (Process.GetCurrentProcess().WorkingSet64, GC.GetTotalMemory(forceFullCollection: false));
    }

    var samples = new List<(long WorkingSet, long Managed)> { Sample() };

    for (var round = 0; round < ScrollRounds; round++)
    {
      // Sweeps through the whole list (not a 2-point bounce) to mirror a long scroll session and
      // force a fresh wave of cell recycling every round.
      var index = round * 17 % FamilyCount;
      await MainThread.InvokeOnMainThreadAsync(() =>
        collectionView.ScrollTo(index, position: ScrollToPosition.MakeVisible, animate: false));
      await Task.Delay(50);

      if (pauseForDump && round is 5 or ScrollRounds - 2)
      {
        await Task.Delay(TimeSpan.FromSeconds(20));
      }

      var sample = Sample();
      samples.Add(sample);

      if (sample.WorkingSet > WorkingSetAbortBytes)
      {
        Assert.Fail(
          $"Aborting at round {round}: working set {sample.WorkingSet / 1_000_000}MB exceeded the " +
          $"{WorkingSetAbortBytes / 1_000_000}MB safety ceiling. pid={pid} samples(WS/managed MB)=" +
          $"[{string.Join(", ", samples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}]");
      }
    }

    // Mirrors "switch to a smaller project and back, repeatedly": rebind the CollectionView to a
    // tiny source (as a project switch would navigate to a fresh page bound to a different, small
    // FamilyInfoItem[]) and back, several times, checking whether each round-trip compounds on the
    // last or whether the footprint settles back to the same place.
    var smallFamilies = Enumerable.Range(0, 3)
      .Select(i => new FamilyInfoItem(N(90_000 + i, $"Small{i}", NameType.FamilyName),
        [P(90_000 + i, $"S{i}", BiologicalSex.Male)], (_, _) => true))
      .ToArray();

    var switchSamples = new List<(long WorkingSet, long Managed)>();
    for (var cycle = 0; cycle < 3; cycle++)
    {
      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = smallFamilies);
      await Task.Delay(300);
      switchSamples.Add(Sample());

      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = families);
      await Task.Delay(300);
      switchSamples.Add(Sample());
    }

    var growth = samples[^1].WorkingSet - samples[0].WorkingSet;
    var report = $"pid={pid} growth={growth / 1_000_000}MB " +
      $"scroll(WS/managed MB)=[{string.Join(", ", samples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}] " +
      $"switch(away,back)x3(WS/managed MB)=[{string.Join(", ", switchSamples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}]";

    // TEMP diagnostic: force a failure to see the sample report regardless of outcome.
    Assert.Fail(report);
  }
}
