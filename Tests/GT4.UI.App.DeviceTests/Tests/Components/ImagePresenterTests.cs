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

  // Aspect set on the presenter's own images would be a local value, and a local value silently
  // outranks the style the caller handed in.
  [Fact]
  public async Task The_callers_style_decides_the_aspect()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var page = new ContentPage();
    await using var window = await WindowHost.AttachAsync(page);
    var services = new TestServices();
    var style = new Style(typeof(Image));
    style.Setters.Add(new Setter { Property = Image.AspectProperty, Value = Aspect.AspectFit });
    var presenter = await MainThread.InvokeOnMainThreadAsync(() => new TestableImagePresenter(services.Provider)
    {
      ImageStyle = style,
      Photos = [new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([1, 2, 3])), null)],
    });

    await MainThread.InvokeOnMainThreadAsync(() => page.Content = presenter);

    var frame = (Grid)presenter.Content;
    var pictures = frame.Children.OfType<Image>().ToArray();
    Assert.NotEmpty(pictures);
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => pictures.All(picture => picture.Aspect == Aspect.AspectFit)),
      applied => applied,
      timeoutMessage: "The caller's style never decided the aspect of the presenter's images.");
  }
}
