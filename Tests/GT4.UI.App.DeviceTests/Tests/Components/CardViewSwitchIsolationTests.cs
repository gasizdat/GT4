using System.Diagnostics;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

// Narrows MemoryGrowthReproTests further: a bare CollectionView (CollectionViewSwitchIsolationTests)
// does not compound across repeated ItemsSource swaps, but the real family-card template (CardView
// wrapping SafeBindableLayout of PersonInfoView) does. This drops CardView specifically -- same
// SafeBindableLayout/PersonInfoView cell content, no GraphicsView shadow wrapper -- to tell whether
// the leak is CardView/GraphicsView or SafeBindableLayout/PersonInfoView.
public class CardViewSwitchIsolationTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex) =>
    new(id, UnknownDate, null, sex, [N(id * 100, firstName, NameType.FirstName)], null);

  [Fact]
  public async Task Repeated_ItemsSource_swaps_without_CardView_do_not_compound()
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
          var view = new PersonInfoView();
          view.SetBinding(PersonInfoView.PersonProperty, new Binding("."));
          return view;
        })
      };
      layout.SetBinding(SafeBindableLayout.ItemsSourceProperty, new Binding(nameof(FamilyInfoItem.DisplayedPersons)));

      var label = new Label();
      label.SetBinding(Label.TextProperty, new Binding($"{nameof(FamilyInfoItem.Info)}.{nameof(Name.Value)}"));

      // No CardView here -- same content, no GraphicsView shadow wrapper.
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

    for (var cycle = 0; cycle < 3; cycle++)
    {
      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = smallFamilies);
      await Task.Delay(300);
      samples.Add(Sample());

      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ItemsSource = families);
      await Task.Delay(300);
      samples.Add(Sample());
    }

    var report = $"pid={Environment.ProcessId} samples(WS/managed MB)=" +
      $"[{string.Join(", ", samples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}]";

    // TEMP diagnostic: force a failure to see the sample report.
    Assert.Fail(report);
  }
}
