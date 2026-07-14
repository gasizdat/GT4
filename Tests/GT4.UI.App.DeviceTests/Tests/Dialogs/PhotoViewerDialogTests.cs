using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers PhotoViewerDialog directly: it has no IServiceProvider/navigation dependency beyond
/// IAlertService, so it's constructed straight from TestServices' mocked container. Callers now
/// hand it ready-made ImageSources (resolved upstream via IDataConverter), so the ctor just holds
/// them as given; the CloseCommand's modal pop is the other thing worth pinning -- the
/// CollectionView/Image layout itself is XAML, not something this harness can assert on.
/// </summary>
public class PhotoViewerDialogTests
{
  private static async Task<PhotoViewerDialog> CreateDialogAsync(TestServices services, ImageSource[]? photos)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new PhotoViewerDialog(photos!, services.AlertService.Object));
  }

  private static async Task<byte[]> ReadBytesAsync(ImageSource source)
  {
    var streamSource = Assert.IsType<StreamImageSource>(source);
    var stream = await streamSource.Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);
    return buffer.ToArray();
  }

  [Fact]
  public async Task Ctor_with_null_photos_yields_an_empty_list()
  {
    var dialog = await CreateDialogAsync(new TestServices(), null);

    Assert.Empty(dialog.Photos);
  }

  [Fact]
  public async Task Ctor_keeps_the_given_photos_in_order()
  {
    byte[] first = [1, 2, 3];
    byte[] second = [4, 5];
    var dialog = await CreateDialogAsync(new TestServices(), [ImageUtils.ImageFromBytes(first), ImageUtils.ImageFromBytes(second)]);

    Assert.Equal(2, dialog.Photos.Length);
    Assert.Equal(first, await ReadBytesAsync(dialog.Photos[0]));
    Assert.Equal(second, await ReadBytesAsync(dialog.Photos[1]));
  }

  [Fact]
  public async Task CloseCommand_pops_the_dialog_off_the_modal_stack()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services, [ImageUtils.ImageFromBytes([1])]);
    var host = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage());
    await using var window = await WindowHost.AttachAsync(host);
    await MainThread.InvokeOnMainThreadAsync(() => host.Navigation.PushModalAsync(dialog));

    await MainThread.InvokeOnMainThreadAsync(() => dialog.CloseCommand.Execute(null));

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => host.Navigation.ModalStack.Count),
      count => count == 0,
      timeoutMessage: "PhotoViewerDialog was not popped off the modal stack.");
  }
}
