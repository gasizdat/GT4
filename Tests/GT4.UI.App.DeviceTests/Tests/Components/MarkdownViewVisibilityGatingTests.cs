using GT4.UI.Components;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

// Pins MarkdownView's hidden-preview gate: no native-tree rebuild (an Image plus wrapper views per
// resolved media link) happens while IsVisible is false, and a catch-up render fires once it
// becomes visible again.
public class MarkdownViewVisibilityGatingTests
{
  private const int MediaLinkCount = 20;

  // A 1x1 PNG: only needs to decode as a real image so ImageUtils.PixelSize gives ScaledImage a
  // non-null size and the resolver takes the same path a real media lookup would.
  private static readonly byte[] SamplePng =
  [
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
    0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
    0x42, 0x60, 0x82,
  ];

  [Fact]
  public async Task Refresh_WhileHidden_BuildsNoImages_ThenCatchesUpOnceShownAgain()
  {
    var markdown = MarkdownWithMediaLinks(MediaLinkCount);
    var mediaSources = MediaSources(MediaLinkCount);

    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var view = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownView
    {
      IsVisible = false,
      MediaResolver = MakeResolver(mediaSources),
      Markdown = markdown,
    });

    // Edit repeatedly while still hidden -- none of it should build a single Image, even once the
    // resolver has answered for every link in the background.
    for (var edit = 0; edit < 5; edit++)
    {
      await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = markdown + $"\n\nEdit {edit}.");
    }

    await MainThread.InvokeOnMainThreadAsync(() => { });
    Assert.Empty(Descendants(view).OfType<Image>());

    await MainThread.InvokeOnMainThreadAsync(() => view.IsVisible = true);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => Descendants(view).OfType<Image>().Count()),
      rendered => rendered == MediaLinkCount,
      timeoutMessage: "The preview never caught up once shown.");
  }

  [Fact]
  public async Task MarkdownEditor_BuildsNoPreviewImages_WhileItsTabIsHidden()
  {
    var markdown = MarkdownWithMediaLinks(MediaLinkCount);
    var mediaSources = MediaSources(MediaLinkCount);

    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var editor = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownEditor
    {
      MediaResolver = MakeResolver(mediaSources),
      Markdown = markdown,
    });

    Assert.True(editor.DisplayEditor);
    Assert.False(editor.DisplayPreview);

    for (var edit = 0; edit < 5; edit++)
    {
      await MainThread.InvokeOnMainThreadAsync(() => editor.Markdown += $"\n\nEdit {edit}.");
    }

    await MainThread.InvokeOnMainThreadAsync(() => { });
    Assert.Empty(Descendants(editor).OfType<Image>());

    await MainThread.InvokeOnMainThreadAsync(() => editor.TabIndex = 1);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => Descendants(editor).OfType<Image>().Count()),
      rendered => rendered == MediaLinkCount,
      timeoutMessage: "The preview never caught up once its tab was shown.");
  }

  private static string MarkdownWithMediaLinks(int count) =>
    string.Concat(Enumerable.Range(1, count).Select(id => $"![caption {id}](media:{id})\n\n"));

  private static Dictionary<string, byte[]> MediaSources(int count) =>
    Enumerable.Range(1, count).ToDictionary(id => $"media:{id}", _ => SamplePng);

  private static InlineMediaResolver MakeResolver(IReadOnlyDictionary<string, byte[]> mediaSources) => link =>
  {
    if (!mediaSources.TryGetValue(link, out var bytes))
    {
      return Task.FromResult<InlineMedia?>(null);
    }

    var source = ImageSource.FromStream(() => new MemoryStream(bytes));
    var pixelSize = ImageUtils.PixelSize(bytes);
    return Task.FromResult<InlineMedia?>(new InlineMedia(source, pixelSize));
  };

  private static IEnumerable<BindableObject> Descendants(BindableObject node)
  {
    foreach (var child in Children(node))
    {
      yield return child;
      foreach (var descendant in Descendants(child))
      {
        yield return descendant;
      }
    }
  }

  private static IEnumerable<BindableObject> Children(BindableObject node)
  {
    switch (node)
    {
      case ContentView { Content: not null } view:
        return [view.Content];
      case Border { Content: not null } border:
        return [border.Content];
      case Layout layout:
        return layout.Children.OfType<BindableObject>();
      case Label { FormattedText: not null } label:
        return label.FormattedText.Spans;
      default:
        return [];
    }
  }
}
