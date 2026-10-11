namespace GT4.UI.Utils;

// A zoomable page's scale. Each method reports whether the scale changed, since a change costs the page a
// full rebuild and a step against a clamp must not cause one.
public sealed class PageZoom
{
  private const double MinScale = 0.4;
  private const double MaxScale = 2.5;
  private const double Step = 0.25;
  private const double DefaultScale = 1.0;
  private const int PinchIntervalMs = 300;

  private long _LastPinchTicks;

  public double Scale { get; private set; } = DefaultScale;

  // A pinch's delta bears no useful relation to a Step, so pace in time and use only its sign.
  public bool Pinch(double delta)
  {
    var ticks = Environment.TickCount64;
    if (ticks - _LastPinchTicks < PinchIntervalMs)
    {
      return false;
    }

    _LastPinchTicks = ticks;
    var direction = Math.Sign(delta);
    return SetScale(Scale + (direction * Step));
  }

  public bool ZoomIn() => SetScale(Scale + Step);

  public bool ZoomOut() => SetScale(Scale - Step);

  // Deliberately not paced: this is a discrete command, never part of a gesture stream.
  public bool Reset() => SetScale(DefaultScale);

  private bool SetScale(double scale)
  {
    var clamped = Math.Clamp(scale, MinScale, MaxScale);
    if (clamped == Scale)
    {
      return false;
    }

    Scale = clamped;
    return true;
  }
}
