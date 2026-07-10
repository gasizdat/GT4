using FluentAssertions;
using GT4.UI.Utils;
using System.Collections.Specialized;
using Xunit;

namespace GT4.UI.View.Tests;

public class FilteredObservableCollectionTests
{
  [Fact]
  public void NoFilter_ItemsNotVisible()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Add(1);
    collection.Add(2);

    collection.Items.Should().BeEmpty("null filter hides everything — set a predicate to show items");
  }

  [Fact]
  public void AllPassFilter_AllItemsVisible()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, _) => true;
    collection.Add(1);
    collection.Add(2);
    collection.Add(3);

    collection.Items.Should().Equal(1, 2, 3);
  }

  [Fact]
  public void SelectiveFilter_OnlyMatchingItemsVisible()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, item) => item > 2;
    collection.AddRange([1, 2, 3, 4]);

    collection.Items.Should().Equal(3, 4);
  }

  [Fact]
  public void SetFilter_AfterAdd_FiltersExistingItems()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, _) => true;
    collection.AddRange([1, 2, 3, 4]);

    collection.Filter = (_, item) => item % 2 == 0;

    collection.Items.Should().Equal(2, 4);
  }

  [Fact]
  public void SetFilterToNull_HidesAllItems()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, _) => true;
    collection.AddRange([1, 2, 3]);

    collection.Filter = null;

    collection.Items.Should().BeEmpty();
  }

  [Fact]
  public void Clear_RemovesAllSourceAndVisibleItems()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, _) => true;
    collection.AddRange([1, 2, 3]);

    collection.Clear();

    collection.Items.Should().BeEmpty();

    collection.Update();
    collection.Items.Should().BeEmpty("source list is also empty after Clear");
  }

  [Fact]
  public void AddRange_AllPassFilter_AllVisible()
  {
    var collection = new FilteredObservableCollection<string>();
    collection.Filter = (_, _) => true;

    collection.AddRange(["alpha", "beta", "gamma"]);

    collection.Items.Should().Equal("alpha", "beta", "gamma");
  }

  [Fact]
  public void AddRange_SelectiveFilter_OnlyMatchingVisible()
  {
    var collection = new FilteredObservableCollection<string>();
    collection.Filter = (_, s) => s.StartsWith("a");

    collection.AddRange(["alpha", "beta", "apple", "cherry"]);

    collection.Items.Should().Equal("alpha", "apple");
  }

  [Fact]
  public void Update_ReappliesFilterWithChangedState()
  {
    var threshold = 0;
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, item) => item > threshold;
    collection.Filter = (_, _) => true;
    collection.AddRange([1, 2, 3]);
    collection.Filter = (_, item) => item > threshold;

    collection.Items.Should().Equal(1, 2, 3);

    threshold = 2;
    collection.Update();

    collection.Items.Should().Equal(3);
  }

  [Fact]
  public void Remove_ExistingItem_RemovesFromBothLists()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, _) => true;
    collection.AddRange([1, 2, 3]);

    var removed = collection.Remove(2);

    removed.Should().BeTrue();
    collection.Items.Should().Equal(1, 3);

    collection.Update();
    collection.Items.Should().Equal([1, 3], "removed item should not reappear after re-filter");
  }

  [Fact]
  public void Count_ReflectsVisibleItemCount()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, item) => item > 2;
    collection.AddRange([1, 2, 3, 4, 5]);

    collection.Count.Should().Be(3);
  }

  [Fact]
  public void Update_RemovesStaleItemsWithoutResetting()
  {
    var threshold = 0;
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, item) => item > threshold;
    collection.AddRange([1, 2, 3, 4, 5]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    collection.Items.CollectionChanged += (_, e) => events.Add(e);

    threshold = 3;
    collection.Update();

    events.Should().NotContain(e => e.Action == NotifyCollectionChangedAction.Reset);
    // Removals are raised highest-index-first (implementation detail of the backward removal pass),
    // so compare as a set rather than asserting a specific order.
    events.SelectMany(e => e.OldItems?.Cast<int>() ?? []).Should().BeEquivalentTo([1, 2, 3]);
    events.SelectMany(e => e.NewItems?.Cast<int>() ?? []).Should().BeEmpty();
    collection.Items.Should().Equal(4, 5);
  }

  [Fact]
  public void Update_InsertsNewlyMatchingItemsWithoutTouchingExisting()
  {
    var showAll = false;
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, item) => showAll || item > 2;
    collection.AddRange([1, 2, 3, 4]);

    var touchedItems = new List<int>();
    collection.Items.CollectionChanged += (_, e) =>
    {
      touchedItems.AddRange(e.OldItems?.Cast<int>() ?? []);
      touchedItems.AddRange(e.NewItems?.Cast<int>() ?? []);
    };

    showAll = true;
    collection.Update();

    touchedItems.Should().Equal(1, 2);
    collection.Items.Should().Equal(1, 2, 3, 4);
  }

  [Fact]
  public void Update_LeavesUnaffectedItemsOutOfAnyEvent()
  {
    var threshold = 0;
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, item) => item > threshold;
    collection.AddRange([1, 2, 3, 4, 5]);

    var touchedItems = new List<int>();
    collection.Items.CollectionChanged += (_, e) =>
    {
      touchedItems.AddRange(e.OldItems?.Cast<int>() ?? []);
      touchedItems.AddRange(e.NewItems?.Cast<int>() ?? []);
    };

    threshold = 3;
    collection.Update();

    touchedItems.Should().NotContain(4);
    touchedItems.Should().NotContain(5);
  }

  [Fact]
  public void AllItems_ReflectsFullSourceRegardlessOfFilter()
  {
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (_, item) => item > 2;
    collection.AddRange([1, 2, 3, 4]);

    collection.Items.Should().Equal(3, 4);
    collection.AllItems.Should().Equal(1, 2, 3, 4);
  }

  [Fact]
  public void FilterReceivesCollectionReference()
  {
    FilteredObservableCollection<int>? capturedCollection = null;
    var collection = new FilteredObservableCollection<int>();
    collection.Filter = (c, _) =>
    {
      capturedCollection = c;
      return true;
    };
    collection.Add(42);

    capturedCollection.Should().BeSameAs(collection);
  }
}
