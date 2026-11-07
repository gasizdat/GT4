using GT4.UI.Components;
using GT4.UI.Items;

namespace GT4.UI.DeviceTests;

internal static class PhotoSlot
{
  // A photo template stacks the picture's grid over the caption row its caption adorner sits in. The
  // stack itself, the CollectionView item's root, never gets a MAUI Frame on Windows, but the picture's
  // grid fills it, so that grid's width is the slot's.
  public static async Task<(double Slot, double Picture)> WidthsAsync(Page dialog, PersonDataItem photo)
  {
    var adorner = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog
        .GetVisualTreeDescendants()
        .OfType<Button>()
        .FirstOrDefault(button => button.CommandParameter is AdornerCommandParameter { CommandName: "EditCaptionCommand" } parameter
          && parameter.Element == photo)),
      found => found is not null,
      timeoutMessage: "The photo's caption adorner never materialized.");

    return await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() =>
      {
        var stack = (Layout)adorner!.Parent.Parent;
        var pictureGrid = (View)stack.Children[0];
        var picture = pictureGrid.GetVisualTreeDescendants().OfType<Image>().First();
        return (pictureGrid.Frame.Width, picture.Frame.Width);
      }),
      widths => widths.Item1 > 0 && widths.Item2 > 0,
      timeoutMessage: "The photo slot was never arranged.");
  }
}
