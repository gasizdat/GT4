using GT4.UI.Items;

namespace GT4.UI.DeviceTests;

internal static class AttachmentRows
{
  public static async Task<ImageSource> WaitForImageThumbnailOnlyAsync(AttachmentItem image, AttachmentItem document)
  {
    var thumbnail = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => image.Thumbnail),
      source => source is not null,
      timeoutMessage: "The scan's thumbnail never resolved.");
    await Poll.ConfirmNeverAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => document.HasThumbnail),
      shown => shown,
      TimeSpan.FromSeconds(1),
      "A document row resolved a thumbnail.");
    return thumbnail!;
  }
}
