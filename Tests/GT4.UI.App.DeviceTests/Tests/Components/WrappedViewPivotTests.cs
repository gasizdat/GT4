#if ANDROID
using Xunit;

namespace GT4.UI.DeviceTests;

public class WrappedViewPivotTests
{
  [Fact]
  public async Task A_wrapped_label_rotated_from_the_start_pivots_on_its_centre()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var label = new Label { Text = "x", Rotation = 90 };
    AbsoluteLayout.SetLayoutBounds(label, new Rect(10, 10, 100, 40));
    var layer = new AbsoluteLayout { InputTransparent = true, Children = { label } };
    var page = new ContentPage { Content = layer };
    await using var window = await WindowHost.AttachAsync(page);

    var (hasContainer, width, height, pivotX, pivotY) = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() =>
      {
        var native = (Android.Views.View)label.Handler!.PlatformView!;
        return (label.Handler.HasContainer, native.Width, native.Height, native.PivotX, native.PivotY);
      }),
      state => state.Width > 0,
      timeoutMessage: "The label was never laid out.");

    Assert.True(hasContainer);
    Assert.InRange(pivotX, (width / 2f) - 1, (width / 2f) + 1);
    Assert.InRange(pivotY, (height / 2f) - 1, (height / 2f) + 1);
  }
}
#endif
