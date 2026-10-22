#if ANDROID
using Android.OS;
using Android.Views;
using Microsoft.Maui.Platform;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Feeds a real touch sequence to the native view MAUI's gesture detector listens on, which is the
/// wrapping container when MAUI wraps the element. Points are in the element's own units.
/// </summary>
internal static class AndroidTouch
{
  public static Task<long> DownAsync(VisualElement element, Point at) =>
    MainThread.InvokeOnMainThreadAsync(() =>
    {
      var now = SystemClock.UptimeMillis();
      Dispatch(element, at, MotionEventActions.Down, now);
      return now;
    });

  public static Task UpAsync(VisualElement element, Point at, long downTime) =>
    MainThread.InvokeOnMainThreadAsync(() => Dispatch(element, at, MotionEventActions.Up, downTime));

  // The long-press timer runs on the main looper, so the wait has to stay off it.
  public static Task OutlastLongPressAsync() => Task.Delay(2 * ViewConfiguration.LongPressTimeout);

  private static void Dispatch(VisualElement element, Point at, MotionEventActions action, long downTime)
  {
    var handler = (IViewHandler)element.Handler!;
    var native = (Android.Views.View)(handler.ContainerView ?? handler.PlatformView)!;
    var x = native.Context!.ToPixels(at.X);
    var y = native.Context.ToPixels(at.Y);
    var eventTime = SystemClock.UptimeMillis();
    using var motion = MotionEvent.Obtain(downTime, eventTime, action, x, y, 0)!;
    native.DispatchTouchEvent(motion);
  }
}
#endif
