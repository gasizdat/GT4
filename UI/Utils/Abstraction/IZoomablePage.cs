namespace GT4.UI.Abstraction;

// A page that draws zoomable content of its own: while it is the current page, the app's zoom gesture
// and hotkeys drive it instead of the global font scale. Zoom deltas arrive in font-scale units, so
// one FontScale.Step is a single hotkey press and a pinch delivers a stream of smaller fractions.
public interface IZoomablePage
{
  void Zoom(double delta);
  void ResetZoom();
}
