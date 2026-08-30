namespace GT4.UI.Behaviors;

public static class ScrollToSelected
{
  public static readonly BindableProperty ScrollToItemProperty =
      BindableProperty.CreateAttached(
          "ScrollToItem",
          typeof(object),
          typeof(ScrollToSelected),
          null,
          propertyChanged: OnScrollToItemChanged);

  public static object GetScrollToItem(BindableObject view) => view.GetValue(ScrollToItemProperty);
  public static void SetScrollToItem(BindableObject view, object value) => view.SetValue(ScrollToItemProperty, value);

  private static void OnScrollToItemChanged(BindableObject bindable, object oldValue, object newValue)
  {
    if (Equals(oldValue, newValue))
      return;

    if (bindable is CollectionView collectionView && newValue != null)
    {
      try
      {
        collectionView.ScrollTo(newValue, position: ScrollToPosition.MakeVisible, animate: true);
      }
      catch (Exception)
      {
        // ScrollTo throws when the selection is not in ItemsSource, which a filter change makes
        // routine; there is nothing to scroll to and nothing to report.
      }
    }
  }
}
