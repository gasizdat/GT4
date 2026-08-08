using GT4.UI.Components;
using GT4.UI.Utils.Converters;
using Xunit;

namespace GT4.UI.DeviceTests;

public class ImagePresenterTests
{
  private sealed class TestableImagePresenter(IServiceProvider serviceProvider) : ImagePresenter(serviceProvider);

  [Fact]
  public async Task Presenter_constructs_before_loading_and_initializes_when_loaded()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var page = new ContentPage();
    await using var window = await WindowHost.AttachAsync(page);
    var services = new TestServices();
    var photo = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([1, 2, 3])), "A photo");
    var presenter = await MainThread.InvokeOnMainThreadAsync(() => new TestableImagePresenter(services.Provider)
    {
      Photos = [photo],
    });

    await MainThread.InvokeOnMainThreadAsync(() => page.Content = presenter);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => presenter.Image1),
      image => ReferenceEquals(image, photo.Source),
      timeoutMessage: "The presenter did not initialize after loading.");

    Assert.Equal(photo.Caption, presenter.CurrentCaption);
  }
}
