using System.Diagnostics;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

// Narrows CardViewSwitchIsolationTests further: that test (SafeBindableLayout of PersonInfoView,
// no CardView) still compounded across repeated outer-CollectionView ItemsSource swaps. This keeps
// SafeBindableLayout bound to the same FamilyInfoItem.DisplayedPersons source but swaps its
// ItemTemplate to a plain Label instead of PersonInfoView, to tell whether the leak is in
// SafeBindableLayout itself or specific to PersonInfoView.
public class SafeBindableLayoutSwitchIsolationTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex) =>
    new(id, UnknownDate, null, sex, [N(id * 100, firstName, NameType.FirstName)], null);

  [Fact]
  public async Task Repeated_ItemsSource_swaps_with_a_plain_Label_template_do_not_compound()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var families = Enumerable.Range(0, 3000)
      .Select(i => new FamilyInfoItem(
        N(i, $"Family{i}", NameType.FamilyName),
        Enumerable.Range(0, 8)
          .Select(j => P(i * 1000 + j, $"P{i}_{j}", j % 2 == 0 ? BiologicalSex.Male : BiologicalSex.Female))
          .ToArray(),
        (_, _) => true))
      .ToArray();

    var smallFamilies = Enumerable.Range(0, 3)
      .Select(i => new FamilyInfoItem(N(90_000 + i, $"Small{i}", NameType.FamilyName),
        [P(90_000 + i, $"S{i}", BiologicalSex.Male)], (_, _) => true))
      .ToArray();

    var template = new DataTemplate(() =>
    {
      var layout = new SafeBindableLayout
      {
        ItemTemplate = new DataTemplate(() =>
        {
          var view = new Label();
          view.SetBinding(Label.TextProperty, new Binding(nameof(PersonInfo.Id)));
          return view;
        })
      };
      layout.SetBinding(SafeBindableLayout.ItemsSourceProperty, new Binding(nameof(FamilyInfoItem.DisplayedPersons)));

      var label = new Label();
      label.SetBinding(Label.TextProperty, new Binding($"{nameof(FamilyInfoItem.Info)}.{nameof(Name.Value)}"));

      return new VerticalStackLayout { Children = { label, layout } };
    });

    var collectionView = new CollectionView { ItemsSource = families, ItemTemplate = template, HeightRequest = 600 };
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

    var pidFile = Path.Combine(Path.GetTempPath(), "gt4_repro_pid.txt");
    File.WriteAllText(pidFile, Environment.ProcessId.ToString());
    var pauseForDump = Environment.GetEnvironmentVariable("GT4_GCDUMP_PAUSE") == "1";

    for (var cycle = 0; cycle < 20; cycle++)
    {
      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = smallFamilies);
      await Task.Delay(100);
      samples.Add(Sample());

      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = families);
      await Task.Delay(100);
      samples.Add(Sample());

      if (pauseForDump && cycle == 6)
      {
        await Task.Delay(TimeSpan.FromSeconds(10));
      }
    }

    var report = $"pid={Environment.ProcessId} samples(WS/managed MB)=" +
      $"[{string.Join(", ", samples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}]";

    // TEMP diagnostic: force a failure to see the sample report.
    Assert.Fail(report);
  }
}
