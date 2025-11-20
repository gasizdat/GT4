using System.Collections;
using System.Collections.ObjectModel;

delegate bool ObservableCollectionFilterPredicate<T>(FilteredObservableCollection<T> collection, T item);

class FilteredObservableCollection<T> : ICollection<T>, ICollection
{
  private readonly List<T> _Items = new();
  private readonly ObservableCollection<T> _InnerCollection = new();
  private ObservableCollectionFilterPredicate<T>? _Filter = null;

  public FilteredObservableCollection()
  {
    _InnerCollection = new();
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

  public int Count => _InnerCollection.Count;

  public bool IsReadOnly => false;

  public bool IsSynchronized => false;

  public object SyncRoot => _Items;

  public void Update()
  {
    _InnerCollection.Clear();
    foreach (var item in _Items)
    {
      if (_Filter?.Invoke(this, item) == true)
      {
        _InnerCollection.Add(item);
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

  public bool Remove(T item)
  {
    Items.Remove(item);
    return _InnerCollection.Remove(item);
  }

  public bool Contains(T item) => _InnerCollection.Contains(item);

  public void CopyTo(T[] array, int arrayIndex) => _InnerCollection.CopyTo(array, arrayIndex);

  public IEnumerator<T> GetEnumerator() => _InnerCollection.GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator() => _InnerCollection.GetEnumerator();

  public void CopyTo(Array array, int index) => _InnerCollection.CopyTo((T[])array, index);
}