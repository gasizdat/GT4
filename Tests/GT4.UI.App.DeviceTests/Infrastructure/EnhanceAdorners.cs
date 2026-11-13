using GT4.UI.Components;
using GT4.UI.Items;

namespace GT4.UI.DeviceTests;

internal static class EnhanceAdorners
{
  // A page constructed detached never realizes its XAML bindings, so the adorner's Element stays null
  // until the page is attached to a window.
  public static Task<Button[]> WaitAsync(Page dialog, IEnumerable<PersonDataItem> photos, string commandName) =>
    Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog
        .GetVisualTreeDescendants()
        .OfType<Button>()
        .Where(button => button.CommandParameter is AdornerCommandParameter { Element: not null } parameter && parameter.CommandName == commandName)
        .ToArray()),
      found => found.Length == photos.Count(),
      timeoutMessage: "The CollectionView's enhance adorners never materialized.");

  // An attachment's adorner hides through its own binding or through the wrapper around it.
  public static bool IsShown(Element element) =>
    element is not VisualElement visual || visual.IsVisible && (element.Parent is null || IsShown(element.Parent));

  // Every adorner is hidden until its attachment's content decodes, so a hidden one means nothing before that.
  public static async Task<(bool Image, bool Document)> AttachmentsShownAsync(
    Page dialog, IEnumerable<PersonDataItem> attachments, string commandName)
  {
    var adorners = await WaitAsync(dialog, attachments, commandName);
    var image = adorners.Single(adorner => Photo(adorner).Info.MimeType == "image/png");
    var document = adorners.Single(adorner => Photo(adorner).Info.MimeType == "application/pdf");
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => attachments.All(attachment => attachment.Content is not null)),
      loaded => loaded,
      timeoutMessage: "The attachments never loaded.");
    return await MainThread.InvokeOnMainThreadAsync(() => (IsShown(image), IsShown(document)));
  }

  public static PersonDataItem Photo(Button adorner) =>
    (PersonDataItem)((AdornerCommandParameter)adorner.CommandParameter).Element;

  public static async Task TapAsync(Page dialog, IEnumerable<PersonDataItem> photos, string commandName, PersonDataItem photo)
  {
    var adorners = await WaitAsync(dialog, photos, commandName);
    var adorner = adorners.Single(button => Photo(button) == photo);
    await MainThread.InvokeOnMainThreadAsync(() => adorner.Command.Execute(adorner.CommandParameter));
  }
}
