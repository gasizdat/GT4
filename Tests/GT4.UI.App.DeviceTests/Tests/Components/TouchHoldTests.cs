#if ANDROID
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// The hold and the tap it suppresses are both decided inside Android's own gesture detector, so only a
/// real touch sequence on a live native view shows either.
/// </summary>
public class TouchHoldTests
{
  private sealed class Counts
  {
    public int Held;
    public int Released;
    public int Tapped;
  }

  private static async Task<(BoxView Target, Counts Counts, IAsyncDisposable Window)> AttachTargetAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var counts = new Counts();
    var target = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var box = new BoxView { WidthRequest = 100, HeightRequest = 100 };
      var tap = new TapGestureRecognizer();
      tap.Tapped += (_, _) => counts.Tapped++;
      var pointer = new PointerGestureRecognizer();
      pointer.PointerReleased += (_, _) => counts.Released++;
      var hold = TouchHold.Create(_ => counts.Held++);
      box.GestureRecognizers.Add(tap);
      box.GestureRecognizers.Add(pointer);
      box.GestureRecognizers.Add(hold);
      return box;
    });
    var page = new ContentPage { Content = new Grid { Children = { target } } };
    var window = await WindowHost.AttachAsync(page);
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => target.Width),
      width => width > 0,
      timeoutMessage: "The target was never laid out.");
    return (target, counts, window);
  }

  [Fact]
  public async Task A_held_finger_raises_the_hold_and_its_lift_a_release_but_no_tap()
  {
    var (target, counts, window) = await AttachTargetAsync();
    await using var _ = window;
    var at = new Point(10, 10);

    var downTime = await AndroidTouch.DownAsync(target, at);
    await AndroidTouch.OutlastLongPressAsync();
    var heldBeforeLift = counts.Held;
    await AndroidTouch.UpAsync(target, at, downTime);

    Assert.Equal(1, heldBeforeLift);
    Assert.Equal(1, counts.Released);
    Assert.Equal(0, counts.Tapped);
  }

  [Fact]
  public async Task A_quick_tap_raises_no_hold()
  {
    var (target, counts, window) = await AttachTargetAsync();
    await using var _ = window;
    var at = new Point(10, 10);

    var downTime = await AndroidTouch.DownAsync(target, at);
    await AndroidTouch.UpAsync(target, at, downTime);
    await AndroidTouch.OutlastLongPressAsync();

    Assert.Equal(0, counts.Held);
    Assert.Equal(1, counts.Tapped);
  }
}
#endif
