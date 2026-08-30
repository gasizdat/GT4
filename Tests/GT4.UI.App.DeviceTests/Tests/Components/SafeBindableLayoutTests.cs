using Microsoft.Maui;
using GT4.UI.Components;
using GT4.UI.Utils;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Xunit;

namespace GT4.UI.DeviceTests;

// Covers Rebuild's reuse-in-place strategy: a whole-source reassignment (the shape a CollectionView
// cell recycling onto a different data item produces) rebinds existing children instead of always
// tearing every one down, since ItemTemplate never varies per item and any child slot is therefore
// interchangeable. PersonInfoViewLeakTests covers the companion leak-safety property (removed
// children still release their platform handler).
public class SafeBindableLayoutTests
{
  private static async Task<SafeBindableLayout> CreateAttachedLayoutAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var layout = new SafeBindableLayout { ItemTemplate = new DataTemplate(() => new Label()) };
    var page = new ContentPage { Content = layout };
    await WindowHost.AttachAsync(page);
    return layout;
  }

  private static async Task<IReadOnlyList<IView>> SetItemsAsync(SafeBindableLayout layout, params string[] items)
  {
    await MainThread.InvokeOnMainThreadAsync(() => layout.ItemsSource = new ObservableCollection<string>(items));
    return await MainThread.InvokeOnMainThreadAsync(() => (IReadOnlyList<IView>)layout.Children.ToList());
  }

  [Fact]
  public async Task Rebuild_reuses_existing_children_when_the_new_source_has_the_same_count()
  {
    var layout = await CreateAttachedLayoutAsync();

    var before = await SetItemsAsync(layout, "A", "B", "C");
    var after = await SetItemsAsync(layout, "X", "Y", "Z");

    Assert.Equal(before, after);
    Assert.Equal(["X", "Y", "Z"], after.Select(c => ((Label)c).BindingContext));
  }

  [Fact]
  public async Task Rebuild_reuses_the_overlap_and_removes_the_surplus_when_the_new_source_is_smaller()
  {
    var layout = await CreateAttachedLayoutAsync();

    var before = await SetItemsAsync(layout, "A", "B", "C");
    var after = await SetItemsAsync(layout, "X");

    Assert.Single(after);
    Assert.Same(before[0], after[0]);
    Assert.Equal("X", ((Label)after[0]).BindingContext);
  }

  [Fact]
  public async Task Rebuild_disconnects_removed_children_handlers()
  {
    var layout = await CreateAttachedLayoutAsync();

    var before = await SetItemsAsync(layout, "A", "B");
    var removed = Assert.IsType<Label>(before[1]);
    var removedHandler = Assert.IsAssignableFrom<IElementHandler>(removed.Handler);

    await SetItemsAsync(layout, "X");

    Assert.Null(removedHandler.VirtualView);
    Assert.Null(removed.Handler);
  }

  [Fact]
  public async Task Rebuild_reuses_the_overlap_and_adds_the_deficit_when_the_new_source_is_larger()
  {
    var layout = await CreateAttachedLayoutAsync();

    var before = await SetItemsAsync(layout, "A");
    var after = await SetItemsAsync(layout, "X", "Y", "Z");

    Assert.Equal(3, after.Count);
    Assert.Same(before[0], after[0]);
    Assert.Equal(["X", "Y", "Z"], after.Select(c => ((Label)c).BindingContext));
  }

  [Fact]
  public async Task A_granular_update_after_a_reuse_based_rebuild_still_targets_the_right_child()
  {
    var layout = await CreateAttachedLayoutAsync();
    await SetItemsAsync(layout, "A", "B", "C");

    var reboundSource = new ObservableCollection<string> { "X", "Y", "Z" };
    await MainThread.InvokeOnMainThreadAsync(() => layout.ItemsSource = reboundSource);
    var reused = await MainThread.InvokeOnMainThreadAsync(() => layout.Children.ToList());

    await MainThread.InvokeOnMainThreadAsync(() => reboundSource.Add("W"));
    var after = await MainThread.InvokeOnMainThreadAsync(() => layout.Children.ToList());

    Assert.Equal(4, after.Count);
    Assert.Equal(reused, after.Take(3));
    Assert.Equal("W", ((Label)after[3]).BindingContext);
  }

  // A filter that hides most of a family's members makes FilteredObservableCollection rebuild with a
  // single Reset instead of granular removals, which reaches Rebuild through the fallback branch.
  [Fact]
  public async Task A_source_reset_rebuilds_the_children_reusing_the_overlap()
  {
    var layout = await CreateAttachedLayoutAsync();
    var visibleUpTo = 4;
    var source = new FilteredObservableCollection<int> { Filter = (_, item) => item <= visibleUpTo };
    source.AddRange([1, 2, 3, 4]);
    await MainThread.InvokeOnMainThreadAsync(() => layout.ItemsSource = source.Items);
    var before = await MainThread.InvokeOnMainThreadAsync(() => layout.Children.ToList());

    var actions = new List<NotifyCollectionChangedAction>();
    source.Items.CollectionChanged += (_, e) => actions.Add(e.Action);
    visibleUpTo = 1;
    await MainThread.InvokeOnMainThreadAsync(source.Update);
    var after = await MainThread.InvokeOnMainThreadAsync(() => layout.Children.ToList());

    Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
    Assert.Equal(4, before.Count);
    Assert.Single(after);
    Assert.Same(before[0], after[0]);
    Assert.Equal(1, ((Label)after[0]).BindingContext);
  }
}
