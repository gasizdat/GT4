using GT4.UI.Components;
using GT4.UI.Items;

namespace GT4.UI.DeviceTests;

internal static class CaptionRows
{
  // A photo template stacks the picture's grid over the caption row. The stack itself, the
  // CollectionView item's root, never gets a MAUI Frame on Windows, but the picture's grid fills it,
  // so that grid's width is the slot's.
  public static async Task<(double Slot, double Picture)> PhotoSlotWidthsAsync(Page dialog, PersonDataItem photo)
  {
    var row = await RowAsync(dialog, photo);

    return await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() =>
      {
        var stack = (Layout)row.Parent;
        var pictureGrid = (View)stack.Children[0];
        var picture = pictureGrid.GetVisualTreeDescendants().OfType<Image>().First();
        return (pictureGrid.Frame.Width, picture.Frame.Width);
      }),
      widths => widths.Item1 > 0 && widths.Item2 > 0,
      timeoutMessage: "The photo slot was never arranged.");
  }

  public static async Task<(double Row, double CaptionRight)> CaptionExtentAsync(Page dialog, PersonDataItem item)
  {
    var row = await RowAsync(dialog, item);

    return await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() =>
      {
        var caption = row.Children.OfType<Label>().Single();
        return (row.Frame.Width, caption.Frame.Right);
      }),
      extent => extent.Item1 > 0 && extent.Item2 > 0,
      timeoutMessage: "The caption row was never arranged.");
  }

  private static async Task<Layout> RowAsync(Page dialog, PersonDataItem item)
  {
    var adorner = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog
        .GetVisualTreeDescendants()
        .OfType<Button>()
        .FirstOrDefault(button => button.CommandParameter is AdornerCommandParameter { CommandName: "EditCaptionCommand" } parameter
          && parameter.Element == item)),
      found => found is not null,
      timeoutMessage: "The item's caption adorner never materialized.");

    return (Layout)adorner!.Parent;
  }
}
