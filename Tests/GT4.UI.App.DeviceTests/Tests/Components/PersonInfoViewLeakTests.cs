using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using System.Collections.ObjectModel;
using Xunit;

namespace GT4.UI.DeviceTests;

// Regression coverage for a confirmed BindableLayout leak: reassigning BindableLayout.ItemsSource to
// a different collection instance -- exactly what happens when a CollectionView cell using this
// template is recycled onto a different family -- never released the views generated for the
// previous source (100% leak rate, confirmed with a plain Label via a WeakReference-tracked stress
// test surviving 8 rounds of GC.Collect(2, Forced, blocking: true, compacting: true)). Matches a
// known class of MAUI issue: dotnet/maui#26042, dotnet/maui discussion#21918. SafeBindableLayout
// fixes it by managing children itself instead of going through BindableLayoutController.
public class PersonInfoViewLeakTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo P(int id, string firstName, BiologicalSex sex = BiologicalSex.Male) =>
    new(id, UnknownDate, null, sex, [N(id * 100, firstName, NameType.FirstName)], null);

  [Fact]
  public async Task SafeBindableLayout_rebound_to_distinct_source_instances_releases_old_views()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var layout = new SafeBindableLayout { ItemTemplate = new DataTemplate(() => new PersonInfoView()) };

    var page = new ContentPage { Content = layout };
    await using var window = await WindowHost.AttachAsync(page);

    var weakRefs = new List<WeakReference>();
    var handlerStates = new List<bool>();

    for (var cycle = 0; cycle < 20; cycle++)
    {
      // A fresh collection instance per cycle mirrors production: each FamilyInfoItem owns its own
      // FilteredObservableCollection<PersonInfo>.
      var id = cycle;
      var source = new ObservableCollection<PersonInfo>([P(id, $"Person{id}")]);

      await MainThread.InvokeOnMainThreadAsync(() => layout.ItemsSource = source);

      var created = await MainThread.InvokeOnMainThreadAsync(() => layout.Children.Cast<PersonInfoView>().ToList());
      weakRefs.AddRange(created.Select(v => new WeakReference(v)));
      handlerStates.AddRange(created.Select(v => v.Handler is not null));
    }

    // Releases the final cycle's view too, so every one of the 20 created views is expected to be
    // collectible -- otherwise the last (still legitimately attached) view would always show as
    // "alive" and mask whether the other 19 actually leaked.
    await MainThread.InvokeOnMainThreadAsync(() => layout.ItemsSource = null);

    // Retried rather than checked once: even a cached, already-decoded default image is bound to its
    // Image control through MAUI's own async image-loading pipeline, so a just-detached view can
    // stay briefly reachable via an in-flight continuation before it settles.
    var aliveCount = weakRefs.Count;
    for (var round = 0; round < 5 && aliveCount > 0; round++)
    {
      await Task.Delay(TimeSpan.FromSeconds(2));
      GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
      GC.WaitForPendingFinalizers();
      GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
      aliveCount = weakRefs.Count(r => r.IsAlive);
    }

    var hadHandlerCount = handlerStates.Count(h => h);

    Assert.True(aliveCount == 0,
      $"{aliveCount} of {weakRefs.Count} PersonInfoView instances leaked via SafeBindableLayout. " +
      $"{hadHandlerCount} of {handlerStates.Count} had a non-null Handler at creation time.");
  }

  [Fact]
  public async Task SafeBindableLayout_leaves_unaffected_children_untouched_on_a_granular_update()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var showAll = false;
    var source = new FilteredObservableCollection<PersonInfo>
    {
      Filter = (_, p) => showAll || p.BiologicalSex == BiologicalSex.Female
    };
    source.AddRange([P(1, "John"), P(2, "Jane", BiologicalSex.Female)]);

    var layout = new SafeBindableLayout
    {
      ItemsSource = source.Items,
      ItemTemplate = new DataTemplate(() =>
      {
        var view = new PersonInfoView();
        view.SetBinding(PersonInfoView.PersonProperty, new Binding("."));
        return view;
      })
    };

    var page = new ContentPage { Content = layout };
    await using var window = await WindowHost.AttachAsync(page);

    var childrenBefore = await MainThread.InvokeOnMainThreadAsync(() => layout.Children.Cast<PersonInfoView>().ToList());
    var janeViewBefore = childrenBefore.Single(v => v.Person!.Id == 2);

    showAll = true;
    await MainThread.InvokeOnMainThreadAsync(() => source.Update());

    var childrenAfter = await MainThread.InvokeOnMainThreadAsync(() => layout.Children.Cast<PersonInfoView>().ToList());
    var janeViewAfter = childrenAfter.Single(v => v.Person!.Id == 2);

    Assert.Same(janeViewBefore, janeViewAfter);
    Assert.Equal(2, childrenAfter.Count);
  }

  // Resolves data.Id == gatedDataId only once Release() is called; every other Data.Id resolves
  // immediately. Models a photo resolution still in flight when SafeBindableLayout.Rebuild reuses the
  // view for a different Person -- reuse means the view is never disconnected, so Handler alone can't
  // tell UpdatePhotoAsync's continuation that its result is stale.
  private sealed class GatedImageConverter(int gatedDataId) : IDataConverter
  {
    private readonly TaskCompletionSource _Gate = new();

    public void Release() => _Gate.SetResult();

    public Task<Data?> FromObjectAsync(object? data, CancellationToken token) => Task.FromResult<Data?>(null);

    public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
    {
      if (data?.Id == gatedDataId)
      {
        await _Gate.Task;
      }
      return data is null ? null : new PhotoInfo(ImageUtils.ImageFromBytes(data.Content), null);
    }
  }

  [Fact]
  public async Task A_reused_views_stale_in_flight_resolution_does_not_overwrite_a_later_Person()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var alicePhoto = new Data(1, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);
    var bobPhoto = new Data(2, [4, 5, 6], "image/png", DataCategory.PersonMainPhoto);
    var converter = new GatedImageConverter(gatedDataId: alicePhoto.Id);

    var services = new ServiceCollection();
    GT4Services.Add(services);
    services.AddKeyedSingleton<IDataConverter>(DataCategory.PersonMainPhoto, converter);
    var provider = services.BuildServiceProvider();

    var view = await MainThread.InvokeOnMainThreadAsync(() => new TestablePersonInfoView(provider));
    var page = new ContentPage { Content = view };
    await using var window = await WindowHost.AttachAsync(page);

    var alice = P(1, "Alice") with { MainPhoto = alicePhoto };
    var bob = P(2, "Bob") with { MainPhoto = bobPhoto };

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.SetValue(PersonInfoView.PersonProperty, alice);
      _ = view.Photo; // starts Alice's resolution, blocked on the gate
    });

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.SetValue(PersonInfoView.PersonProperty, bob);
      _ = view.Photo; // starts Bob's own (ungated) resolution
    });
    await Task.Delay(300); // let Bob's resolution complete
    var bobResolvedPhoto = await MainThread.InvokeOnMainThreadAsync(() => view.Photo);

    converter.Release();
    await Task.Delay(300); // give Alice's now-unblocked continuation a chance to run

    var photoAfterStaleContinuation = await MainThread.InvokeOnMainThreadAsync(() => view.Photo);

    Assert.Same(bobResolvedPhoto, photoAfterStaleContinuation);
  }
}
