using System.Collections;
using System.Collections.ObjectModel;

namespace GT4.UI.Utils;

public delegate bool ObservableCollectionFilterPredicate<T>(FilteredObservableCollection<T> collection, T item);

public class FilteredObservableCollection<T> : ICollection<T>, ICollection
{
  private readonly List<T> _Items = new();
  private readonly ObservableCollection<T> _InnerCollection = new();
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

  // A positional remove-then-insert merge, not a full diff: unaffected items raise no
  // CollectionChanged event. This is only correct because _Items is never reordered in place --
  // Add/AddRange/InsertRange only add, RemoveRange only removes a contiguous run, and Clear wipes it
  // -- which guarantees two filter passes always agree on the relative order of any item present in
  // both. Do not add a method that reorders _Items without revisiting this.
  public void Update()
  {
    var matched = _Items.Where(item => _Filter?.Invoke(this, item) == true).ToList();
    var matchedSet = new HashSet<T>(matched);

    for (var i = _InnerCollection.Count - 1; i >= 0; i--)
    {
      if (!matchedSet.Contains(_InnerCollection[i]))
      {
        _InnerCollection.RemoveAt(i);
      }
    }

    for (var i = 0; i < matched.Count; i++)
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

  IEnumerator IEnumerable.GetEnumerator() => _InnerCollection.GetEnumerator();

  public void CopyTo(Array array, int index) => _InnerCollection.CopyTo((T[])array, index);
}
