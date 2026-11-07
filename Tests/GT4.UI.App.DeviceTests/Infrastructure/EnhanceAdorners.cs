using GT4.UI.Components;
using GT4.UI.Items;

namespace GT4.UI.DeviceTests;

internal static class EnhanceAdorners
{
  // A page constructed detached never realizes its XAML bindings, so the adorner's Element stays null
  // until the page is attached to a window.
  public static Task<Button[]> WaitAsync(Page dialog, ICollection<PersonDataItem> photos, string commandName) =>
    Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog
        .GetVisualTreeDescendants()
        .OfType<Button>()
        .Where(button => button.CommandParameter is AdornerCommandParameter { Element: not null } parameter && parameter.CommandName == commandName)
        .ToArray()),
      found => found.Length == photos.Count,
      timeoutMessage: "The CollectionView's enhance adorners never materialized.");

  // An attachment's adorner hides through its own binding or through the wrapper around it.
  public static bool IsShown(Element element) =>
    element is not VisualElement visual || visual.IsVisible && (element.Parent is null || IsShown(element.Parent));

  public static PersonDataItem Photo(Button adorner) =>
    (PersonDataItem)((AdornerCommandParameter)adorner.CommandParameter).Element;

  public static async Task TapAsync(Page dialog, ICollection<PersonDataItem> photos, string commandName, PersonDataItem photo)
  {
    var adorners = await WaitAsync(dialog, photos, commandName);
    var adorner = adorners.Single(button => Photo(button) == photo);
    await MainThread.InvokeOnMainThreadAsync(() => adorner.Command.Execute(adorner.CommandParameter));
  }
}
