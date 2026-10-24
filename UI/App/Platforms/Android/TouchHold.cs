namespace GT4.UI;

// Android runs its long-press only for a view carrying a drag recognizer. Cancelled, the drag never
// starts, and the lift that ends the hold raises a pointer release instead of a tap.
static class TouchHold
{
  public static DragGestureRecognizer Create(Action<DragStartingEventArgs> held)
  {
    var hold = new DragGestureRecognizer();
    hold.DragStarting += (_, e) =>
    {
      e.Cancel = true;
      held(e);
    };
    return hold;
  }
}
