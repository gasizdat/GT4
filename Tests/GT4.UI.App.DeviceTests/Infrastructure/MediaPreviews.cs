using GT4.UI.Components;
using GT4.UI.Items;

namespace GT4.UI.DeviceTests;

internal static class MediaPreviews
{
  public const string CommandName = "PreviewMediaCommand";

  // Like an adorner's, a recognizer's Element stays null until the page is attached to a window.
  public static Task<(View Owner, TapGestureRecognizer Tap)[]> WaitAsync(Page dialog, IEnumerable<PersonDataItem> items) =>
    Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => All(dialog).Where(preview => items.Contains(Item(preview.Tap))).ToArray()),
      found => found.Length == items.Count(),
      timeoutMessage: "The CollectionView's preview taps never materialized.");

  public static (View Owner, TapGestureRecognizer Tap)[] Taps(Page page) =>
    [.. page
      .GetVisualTreeDescendants()
      .OfType<View>()
      .SelectMany(view => view.GestureRecognizers.OfType<TapGestureRecognizer>().Select(tap => (view, tap)))];

  public static PersonDataItem Item(TapGestureRecognizer tap) =>
    (PersonDataItem)((AdornerCommandParameter)tap.CommandParameter!).Element;

  public static async Task TapAsync(Page dialog, IEnumerable<PersonDataItem> items, PersonDataItem item)
  {
    var previews = await WaitAsync(dialog, items);
    var tap = previews.Single(preview => Item(preview.Tap) == item).Tap;
    await MainThread.InvokeOnMainThreadAsync(() => tap.Command!.Execute(tap.CommandParameter));
  }

  private static IEnumerable<(View Owner, TapGestureRecognizer Tap)> All(Page page) =>
    Taps(page).Where(preview => preview.Tap.CommandParameter is AdornerCommandParameter { Element: not null, CommandName: CommandName });
}
