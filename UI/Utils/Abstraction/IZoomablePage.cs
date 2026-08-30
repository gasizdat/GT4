namespace GT4.UI.Abstraction;

// While such a page is the current one, the app's zoom gesture and hotkeys drive it instead of the
// global font scale. Deltas are in font-scale units: one FontScale.Step per hotkey press,
// arbitrarily smaller fractions from a pinch.
public interface IZoomablePage
{
  void Zoom(double delta);
  void ResetZoom();
}
