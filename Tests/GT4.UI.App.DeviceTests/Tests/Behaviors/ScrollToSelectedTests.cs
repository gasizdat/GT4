using GT4.UI.Behaviors;
using Xunit;

namespace GT4.UI.DeviceTests;

public class ScrollToSelectedTests
{
  private static readonly string[] Items = ["a", "b", "c"];

  [Fact]
  public async Task Selecting_an_item_outside_the_ItemsSource_does_not_throw()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var collectionView = new CollectionView { ItemsSource = Items };
    var page = new ContentPage { Content = collectionView };
    await using var window = await WindowHost.AttachAsync(page);

    Action selectMissing = () => ScrollToSelected.SetScrollToItem(collectionView, "missing");
    var exception = await MainThread.InvokeOnMainThreadAsync(() => Record.Exception(selectMissing));

    Assert.Null(exception);
  }
}
