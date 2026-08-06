using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

// Stress coverage for a reported crash: scrolling ProjectPage's family list up/down a handful of
// cards crashed AppWinOnly.exe with a native fault, 0xC000027B (STATUS_STOWED_EXCEPTION -- a
// WinRT/C++-WinRT exception that crossed a native ABI boundary uncaught), even after capping each
// card at 30 persons, so it wasn't the giant-card issue. This test does not reliably reproduce the
// crash itself (cell recycling and native handler creation/teardown are not deterministically
// timed outside the real app), so its passing is not proof the underlying issue is fixed -- it
// exercises the same shape of interaction (rapid ItemsSource reassignment racing in-flight async
// photo loads) that SafeBindableLayout.ScheduleRebuild's deferral targets: never mutate
// FlexLayout.Children synchronously from inside CollectionView's own native cell-recycle callback.
public class ScrollCrashReproTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  // A non-null MainPhoto routes PersonInfoView.Photo through the async SafeTask.Run/
  // ResolvePhotoAsync path (empty content is fine -- ImageFromBytes falls back to a built-in
  // transparent PNG for zero-length data; only reaching the async branch matters here).
  private static PersonInfo P(int id, string firstName) =>
    new(id, UnknownDate, null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName)],
      new Data(id, [], "image/png", DataCategory.PersonMainPhoto));

  [Fact]
  public async Task Scrolling_a_CollectionView_of_SafeBindableLayout_cards_up_and_down_does_not_crash()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var families = Enumerable.Range(1, 60)
      .Select(i => new FamilyInfoItem(
        N(i, $"Family{i}", NameType.FamilyName),
        Enumerable.Range(1, 5).Select(j => P(i * 100 + j, $"Person{i}_{j}")).ToArray(),
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
      layout.SetBinding(SafeBindableLayout.ItemsSourceProperty, new Binding(nameof(FamilyInfoItem.Persons)));
      return layout;
    });

    var collectionView = new CollectionView
    {
      ItemsSource = families,
      ItemTemplate = template,
      HeightRequest = 400,
    };

    var page = new ContentPage { Content = collectionView };
    await using var window = await WindowHost.AttachAsync(page);

    await MainThread.InvokeOnMainThreadAsync(() => { });
    await Task.Delay(500);

    // Mirrors "scroll up and down a bit (+/- 5-6 families)": bounce the scroll position back and
    // forth across a handful of cards repeatedly, forcing cell recycling each time. No settle delay
    // between bounces -- deliberately racing each recycle against the previous cells' still-in-
    // flight async photo loads, which is the suspected trigger.
    for (var round = 0; round < 30; round++)
    {
      var index = round % 2 == 0 ? 6 : 0;
      await MainThread.InvokeOnMainThreadAsync(() => collectionView.ScrollTo(index, position: ScrollToPosition.MakeVisible, animate: false));
    }

    var stillAttached = await MainThread.InvokeOnMainThreadAsync(() => collectionView.Handler is not null);
    Assert.True(stillAttached, "CollectionView lost its handler -- likely crashed or was torn down mid-scroll.");
  }
}
