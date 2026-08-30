using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GT4.UI.Utils;

public delegate bool ObservableCollectionFilterPredicate<T>(FilteredObservableCollection<T> collection, T item);

public class FilteredObservableCollection<T> : ICollection<T>, ICollection
{
  // ObservableCollection<T> has no bulk-populate API, so filling it item by item fires N separate
  // Insert notifications. This fires a single Reset instead.
  private sealed class BulkObservableCollection<TItem> : ObservableCollection<TItem>
  {
    public void ReplaceAll(IEnumerable<TItem> items)
    {
      Items.Clear();
      foreach (var item in items)
      {
        Items.Add(item);
      }
      OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
      OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
      OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
  }

  private readonly List<T> _Items = new();
  private readonly BulkObservableCollection<T> _InnerCollection = new();
  private ObservableCollectionFilterPredicate<T>? _Filter = null;

  public FilteredObservableCollection()
  {
  }

  public ObservableCollectionFilterPredicate<T>? Filter
  {
    get => _Filter;
    set
    {
      _Filter = value;
      Update();
    }
  }

  public ObservableCollection<T> Items => _InnerCollection;

  // The full, unfiltered source list -- lets a consumer reach items the current filter hides (e.g.
  // to re-evaluate a nested filter on all of them, not just the currently-visible ones).
  public IReadOnlyList<T> AllItems => _Items;

  public int Count => _InnerCollection.Count;

  public bool IsReadOnly => false;

  public bool IsSynchronized => false;

  public object SyncRoot => _Items;

  // The positional merge is only correct because _Items is never reordered in place --
  // Add/AddRange/InsertRange only add, RemoveRange only removes a contiguous run, and Clear wipes it
  // -- so two filter passes always agree on the relative order of any item present in both. Do not
  // add a method that reorders _Items without revisiting this.
  public void Update()
  {
    var matched = _Items.Where(item => _Filter?.Invoke(this, item) == true).ToArray();
    var matchedSet = new HashSet<T>(matched);
    var survivorsCount = _InnerCollection.Count(matchedSet.Contains);
    var removalsCount = _InnerCollection.Count - survivorsCount;
    var insertionsCount = matched.Length - survivorsCount;
    var touchedCount = removalsCount + insertionsCount;

    // Ascending inserts land inside the CollectionView's realized viewport, so virtualization does
    // not spare them: restoring N items one notification at a time costs N times what one Reset does.
    if (touchedCount > survivorsCount)
    {
      _InnerCollection.ReplaceAll(matched);
      return;
    }

    for (var i = _InnerCollection.Count - 1; i >= 0; i--)
    {
      if (!matchedSet.Contains(_InnerCollection[i]))
      {
        _InnerCollection.RemoveAt(i);
      }
    }

    for (var i = 0; i < matched.Length; i++)
    {
      if (i >= _InnerCollection.Count || !EqualityComparer<T>.Default.Equals(_InnerCollection[i], matched[i]))
      {
        _InnerCollection.Insert(i, matched[i]);
      }
    }
  }

  public void Clear()
  {
    _Items.Clear();
    _InnerCollection.Clear();
  }

  public void Add(T item)
  {
    _Items.Add(item);
    Update();
  }

  public void AddRange(IEnumerable<T> collection)
  {
    _Items.AddRange(collection);
    Update();
  }

  // Master-relative index -- lets a consumer that maintains its own positional structure over
  // AllItems (e.g. a flattened tree) find where an item sits regardless of what the filter hides.
  public int IndexOf(T item) => _Items.IndexOf(item);

  public void InsertRange(int index, IEnumerable<T> items)
  {
    _Items.InsertRange(index, items);
    Update();
  }

  public void RemoveRange(int index, int count)
  {
    _Items.RemoveRange(index, count);
    Update();
  }

  public bool Remove(T item)
  {
    _Items.Remove(item);
    return _InnerCollection.Remove(item);
  }

  public bool Contains(T item) => _InnerCollection.Contains(item);

  public void CopyTo(T[] array, int arrayIndex) => _InnerCollection.CopyTo(array, arrayIndex);

  public IEnumerator<T> GetEnumerator() => _InnerCollection.GetEnumerator();

  public void CopyTo(Array array, int index) => _InnerCollection.CopyTo((T[])array, index);

  IEnumerator IEnumerable.GetEnumerator() => _InnerCollection.GetEnumerator();
}
