using System.Diagnostics;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

// Discriminator #2: ProductionRefreshIsolationTests showed that mutating one stable ItemsSource
// in place (what ProjectPage.RefreshView() actually does) does not compound. But "switch to a
// different project" is a page-level navigation, not a data refresh -- it tears down the whole
// ProjectPage (and its CollectionView) and creates a new one for the new project. This mirrors
// that: two full pages, each with the real CardView/SafeBindableLayout/PersonInfoView family-card
// template (matching ProjectPage.xaml, not a stripped-down stand-in), swapped in as Window.Page
// repeatedly -- the same mechanism Window.OnPageChanged uses for ordinary navigation.
public class PageSwitchIsolationTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex) =>
    new(id, UnknownDate, null, sex, [N(id * 100, firstName, NameType.FirstName)], null);

  private static ContentPage BuildFamilyListPage(FamilyInfoItem[] families)
  {
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

    var collectionView = new CollectionView { ItemsSource = families, ItemTemplate = template, HeightRequest = 600 };
    return new ContentPage { Content = collectionView };
  }

  [Fact]
  public async Task Navigating_between_a_large_and_small_project_page_does_not_compound()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var families = Enumerable.Range(0, 1500)
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

    var largePage = BuildFamilyListPage(families);
    var smallPage = BuildFamilyListPage(smallFamilies);

    var window = Application.Current!.Windows[0];
    var originalPage = window.Page!;

    await MainThread.InvokeOnMainThreadAsync(() => window.Page = largePage);
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

    try
    {
      for (var cycle = 0; cycle < 6; cycle++)
      {
        await MainThread.InvokeOnMainThreadAsync(() => window.Page = smallPage);
        await Task.Delay(300);
        samples.Add(Sample());

        await MainThread.InvokeOnMainThreadAsync(() => window.Page = largePage);
        await Task.Delay(300);
        samples.Add(Sample());

        if (pauseForDump && cycle == 3)
        {
          await Task.Delay(TimeSpan.FromSeconds(15));
        }
      }
    }
    finally
    {
      // Restore the runner's own page so subsequent tests in the same host still have a Window.Page
      // to attach to (mirrors WindowHost.Attachment's own restore step).
      for (var attempt = 1; attempt <= 3; attempt++)
      {
        try
        {
          await MainThread.InvokeOnMainThreadAsync(() => window.Page = originalPage);
          break;
        }
        catch (System.Runtime.InteropServices.COMException) when (attempt < 3)
        {
          await Task.Delay(50);
        }
      }
    }

    var report = $"pid={Environment.ProcessId} samples(WS/managed MB)=" +
      $"[{string.Join(", ", samples.Select(s => $"{s.WorkingSet / 1_000_000}/{s.Managed / 1_000_000}"))}]";

    // TEMP diagnostic: force a failure to see the sample report.
    Assert.Fail(report);
  }
}
