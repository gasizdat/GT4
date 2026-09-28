using GT4.UI.Behaviors;
using Xunit;

namespace GT4.UI.DeviceTests;

public class FadeVisibilityBehaviorTests
{
  [Fact]
  public async Task Showing_again_during_the_fade_out_leaves_the_element_visible()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var behavior = new FadeVisibilityBehavior { IsVisible = true, Duration = 500 };
    var element = new BoxView { Behaviors = { behavior } };
    var page = new ContentPage { Content = element };
    await using var window = await WindowHost.AttachAsync(page);

    await MainThread.InvokeOnMainThreadAsync(() => behavior.IsVisible = false);
    await Task.Delay(100);
    await MainThread.InvokeOnMainThreadAsync(() => behavior.IsVisible = true);
    await Task.Delay(1000);

    var isVisible = await MainThread.InvokeOnMainThreadAsync(() => element.IsVisible);
    Assert.True(isVisible, "The fade-out that was interrupted hid the element anyway.");
  }
}
