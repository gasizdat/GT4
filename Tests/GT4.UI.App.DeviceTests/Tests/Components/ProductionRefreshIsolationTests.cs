using System.Collections.ObjectModel;
using System.Diagnostics;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

// Discriminator: SafeBindableLayoutSwitchIsolationTests showed compounding managed-heap growth when
// the OUTER CollectionView.ItemsSource is reassigned to a different array instance every cycle. But
// ProjectPage never does that -- ViewUtils.RefreshView() just re-raises PropertyChanged, and the
// bound Families collection instance never changes identity; EnsureFamiliesLoaded's Refresh() path
// does Clear() + AddRange() on the SAME collection. This mirrors that exact pattern -- one
// ObservableCollection, set as ItemsSource once, mutated via Clear()+Add() -- to tell whether the
// compounding reproduces production's actual refresh mechanism or only the ItemsSource-reassignment
// mechanism the other test used.
public class ProductionRefreshIsolationTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex) =>
    new(id, UnknownDate, null, sex, [N(id * 100, firstName, NameType.FirstName)], null);

  [Fact]
  public async Task Clear_and_re_Add_on_the_same_ItemsSource_instance_do_not_compound()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var families = Enumerable.Range(0, 300)
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

    // Set once and never reassigned -- mirrors ProjectPage.Families always returning the same
    // _Families.Items instance, refreshed via RefreshView() (a PropertyChanged re-raise) rather than
    // a new ItemsSource value.
    var source = new ObservableCollection<FamilyInfoItem>(families);
    var collectionView = new CollectionView { ItemsSource = source, ItemTemplate = template, HeightRequest = 600 };
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

    for (var cycle = 0; cycle < 8; cycle++)
    {
      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        source.Clear();
        foreach (var f in smallFamilies)
        {
          source.Add(f);
        }
      });
      await Task.Delay(100);
      samples.Add(Sample());

      await MainThread.InvokeOnMainThreadAsync(() =>
      {
        source.Clear();
        foreach (var f in families)
        {
          source.Add(f);
        }
      });
      await Task.Delay(100);
      samples.Add(Sample());
    }

    var report = $"pid={Environment.ProcessId} samples(WS/managed MB)=" +
      $"[{string.Join(", ", samples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}]";

    // TEMP diagnostic: force a failure to see the sample report.
    Assert.Fail(report);
  }
}
