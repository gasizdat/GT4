using GT4.UI.Components;
using GT4.UI.Utils.Converters;
using Xunit;

namespace GT4.UI.DeviceTests;

public class ImagePresenterTests
{
  private const double PictureSize = 120;

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

  [Fact]
  public async Task A_long_caption_does_not_widen_the_presenter()
  {
    var brief = await PresenterWidthAsync("A photo");
    var attribution = await PresenterWidthAsync(
      "Charlotte Brontë, engraved by James Charles Armytage after George Richmond, c. 1850. "
      + "Public domain, via Wikimedia Commons.");

    Assert.Equal(brief, attribution, 1);
  }

  [Fact]
  public async Task The_picture_decides_the_presenters_width()
  {
    var width = await PresenterWidthAsync("A photo");

    Assert.Equal(PictureSize, width, 1);
  }

  // Stacked rather than dropped straight into the page: a stack takes its width from what it holds,
  // which is how the real hosts measure this and the only arrangement where a caption can push wider.
  private static async Task<double> PresenterWidthAsync(string caption)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var services = new TestServices();
    var style = new Style(typeof(Image));
    style.Setters.Add(new Setter { Property = VisualElement.WidthRequestProperty, Value = PictureSize });
    style.Setters.Add(new Setter { Property = VisualElement.HeightRequestProperty, Value = PictureSize });
    var photo = new PhotoInfo(ImageSource.FromStream(() => new MemoryStream([1, 2, 3])), caption);
    var presenter = await MainThread.InvokeOnMainThreadAsync(() => new TestableImagePresenter(services.Provider)
    {
      ImageStyle = style,
      Photos = [photo],
    });

    var page = await MainThread.InvokeOnMainThreadAsync(
      () => new ContentPage { Content = new HorizontalStackLayout { presenter } });

    await using var window = await WindowHost.AttachAsync(page);

    var picture = ((Grid)presenter.Content).Children.OfType<Image>().First();
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => picture.Frame.Width),
      arranged => arranged > 0,
      timeoutMessage: "The presenter's picture was never arranged.");

    return await MainThread.InvokeOnMainThreadAsync(() => presenter.Frame.Width);
  }
}
