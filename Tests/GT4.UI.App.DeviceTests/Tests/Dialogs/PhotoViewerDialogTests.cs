using GT4.UI.Dialogs;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers PhotoViewerDialog directly: it has no IServiceProvider/navigation dependency beyond
/// IAlertService, so it's constructed straight from TestServices' mocked container. The ctor's
/// empty-photo filtering and the CloseCommand's modal pop are the only things it owns worth pinning
/// -- the CollectionView/Image layout itself is XAML, not something this harness can assert on.
/// </summary>
public class PhotoViewerDialogTests
{
  private static async Task<PhotoViewerDialog> CreateDialogAsync(TestServices services, byte[][]? photos)
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
  public async Task Ctor_drops_empty_photos_and_keeps_the_rest_in_order()
  {
    byte[] first = [1, 2, 3];
    byte[] second = [4, 5];
    var dialog = await CreateDialogAsync(new TestServices(), [first, [], second]);

    Assert.Equal(2, dialog.Photos.Length);
    Assert.Equal(first, await ReadBytesAsync(dialog.Photos[0]));
    Assert.Equal(second, await ReadBytesAsync(dialog.Photos[1]));
  }

  [Fact]
  public async Task CloseCommand_pops_the_dialog_off_the_modal_stack()
  {
    var services = new TestServices();
    var dialog = await CreateDialogAsync(services, [[1]]);
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
