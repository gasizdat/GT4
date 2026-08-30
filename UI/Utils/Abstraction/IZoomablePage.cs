namespace GT4.UI.Abstraction;

// While such a page is the current one, the app's zoom gesture and hotkeys drive it instead of the
// global font scale. A pinch calls Zoom continuously as the fingers move, so an implementation that
// redraws per step must pace itself; delta is a font-scale-sized amount, one Step per hotkey press.
public interface IZoomablePage
{
  void Zoom(double delta);
  void ResetZoom();
}
