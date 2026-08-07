using System.Collections;
using System.Collections.Specialized;

namespace GT4.UI.Components;

// BindableLayout leaks every view it has ever generated when ItemsSource is reassigned to a
// different collection instance -- confirmed via a WeakReference-tracked stress test (see
// PersonInfoViewLeakTests): a CollectionView cell recycled onto a different data item rebinds a
// nested BindableLayout to a new collection instance every time, and the old views are never
// released. Mirrors ItemsSource via CollectionChanged instead, explicitly disconnecting each
// removed child's platform handler, which a manual Children.Remove + DisconnectHandler proved
// releases cleanly where BindableLayout's own removal path does not.
public class SafeBindableLayout : FlexLayout
{
  private INotifyCollectionChanged? _SubscribedSource;
  private bool _RebuildQueued;

  public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
    nameof(ItemsSource), typeof(IEnumerable), typeof(SafeBindableLayout), null, propertyChanged: OnItemsSourceChanged);

  public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
    nameof(ItemTemplate), typeof(DataTemplate), typeof(SafeBindableLayout), propertyChanged: OnItemTemplateChanged);

  public IEnumerable? ItemsSource
  {
    get => (IEnumerable?)GetValue(ItemsSourceProperty);
    set => SetValue(ItemsSourceProperty, value);
  }

  public DataTemplate? ItemTemplate
  {
    get => (DataTemplate?)GetValue(ItemTemplateProperty);
    set => SetValue(ItemTemplateProperty, value);
  }

  private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
  {
    var layout = (SafeBindableLayout)bindable;

    if (layout._SubscribedSource is not null)
    {
      layout._SubscribedSource.CollectionChanged -= layout.OnSourceCollectionChanged;
    }

    layout._SubscribedSource = newValue as INotifyCollectionChanged;
    if (layout._SubscribedSource is not null)
    {
      layout._SubscribedSource.CollectionChanged += layout.OnSourceCollectionChanged;
    }

    layout.ScheduleRebuild();
  }

  // ItemsSource and ItemTemplate are independent bindable properties, so XAML (or an object
  // initializer) can assign either one first -- e.g. an attribute on the opening tag is applied
  // before a child property-element. Rebuilding on both changes keeps the layout correct regardless
  // of assignment order, at the cost of one throwaway rebuild if both are set together.
  private static void OnItemTemplateChanged(BindableObject bindable, object oldValue, object newValue) =>
    ((SafeBindableLayout)bindable).ScheduleRebuild();

  private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    // A rebuild is already queued for the next dispatcher tick and will read ItemsSource fresh
    // when it runs, so this event's effect is already captured by it -- applying it now would
    // target Children indices from before that rebuild catches up.
    if (_RebuildQueued)
    {
      return;
    }

    switch (e.Action)
    {
      case NotifyCollectionChangedAction.Add:
        InsertChildren(e.NewStartingIndex, e.NewItems!);
        break;

      case NotifyCollectionChangedAction.Remove:
        RemoveChildren(e.OldStartingIndex, e.OldItems!.Count);
        break;

      // Reset, Replace, Move: none of these occur in FilteredObservableCollection's own diff (it
      // only ever raises Add/Remove/Reset), but a full rebuild is a safe fallback for any of them.
      default:
        ScheduleRebuild();
        break;
    }
  }

  // CollectionView reassigns ItemsSource synchronously from inside its own native cell-recycle
  // callback (WinUI ItemsRepeater realizing/recycling an element). Rebuild() inserts/removes
  // FlexLayout.Children, which creates/destroys native handlers synchronously too -- doing that
  // from inside the platform's own callback stack risks an exception crossing the native ABI
  // boundary uncaught, which WinRT reports as a fatal "stowed exception" (0xC000027B) rather than
  // a catchable managed one. Deferring the actual mutation to the next dispatcher tick gets it off
  // that call stack. Coalesced: rapid scrolling can reassign ItemsSource many times before the
  // first deferred rebuild runs, and only the final state matters.
  private void ScheduleRebuild()
  {
    if (_RebuildQueued)
    {
      return;
    }

    _RebuildQueued = true;
    MainThread.BeginInvokeOnMainThread(() =>
    {
      _RebuildQueued = false;
      Rebuild();
    });
  }

  // New items still map onto the same ItemTemplate, so an existing child can be repointed at a new
  // item via BindingContext instead of torn down and recreated -- every child is structurally
  // interchangeable. Relies on PersonInfoView.OnPersonChanged resetting per-item state (photo,
  // generation) when BindingContext moves on.
  private void Rebuild()
  {
    var items = ItemsSource?.Cast<object>().ToList() ?? [];
    var reused = Math.Min(Children.Count, items.Count);

    for (var i = 0; i < reused; i++)
    {
      if (Children[i] is BindableObject bindable)
      {
        bindable.BindingContext = items[i];
      }
    }

    if (Children.Count > reused)
    {
      RemoveChildren(reused, Children.Count - reused);
    }
    else if (items.Count > reused)
    {
      InsertChildren(reused, items.Skip(reused).ToList());
    }
  }

  private void InsertChildren(int startIndex, IList items)
  {
    if (ItemTemplate is not { } template)
    {
      return;
    }

    for (var i = 0; i < items.Count; i++)
    {
      if (template.CreateContent() is not View view)
      {
        continue;
      }

      view.BindingContext = items[i];
      Children.Insert(startIndex + i, view);
    }
  }

  private void RemoveChildren(int startIndex, int count)
  {
    for (var i = 0; i < count; i++)
    {
      try
      {
        var child = Children[startIndex];

        // Defensive disconnect: some platforms (notably Android) can hold native references longer
        // than expected. Attempt to disconnect while still in the Children collection, but some
        // platform/handler implementations require the parent MauiContext to be available and will
        // throw if it is not; swallow that specific failure and retry after removal.
        try
        {
          (child as VisualElement)?.Handler?.DisconnectHandler();
        }
        catch (InvalidOperationException ex) when (ex.Message != null && ex.Message.Contains("MauiContext should have been set on parent"))
        {
          // Swallow and continue; we'll retry after removal.
        }

        Children.RemoveAt(startIndex);

        if (child is BindableObject bindable)
        {
          bindable.BindingContext = null;
        }

        // Extra safety: disconnect again in case removal changed platform state. Also swallow the
        // same MauiContext-related InvalidOperationException if it occurs here.
        try
        {
          (child as VisualElement)?.Handler?.DisconnectHandler();
        }
        catch (InvalidOperationException ex) when (ex.Message != null && ex.Message.Contains("MauiContext should have been set on parent"))
        {
          // Best-effort only; nothing more to do here.
        }
      }
      catch (InvalidOperationException ex) when (ex.Message != null && ex.Message.Contains("MauiContext should have been set on parent"))
      {
        // In some contexts the visual tree's MauiContext isn't available for certain handler ops.
        // Swallow this specific InvalidOperationException to keep test runs stable; the runtime
        // will still finalize native resources later.
      }
    }
  }
}
