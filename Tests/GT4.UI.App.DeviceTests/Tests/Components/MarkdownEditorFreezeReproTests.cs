using GT4.UI.Components;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

// Repro/diagnostic coverage for issue #422 (editor lags/freezes on large biographies with many
// media links). The issue's own investigation (stack captures + code reading) landed on a specific
// mechanism: MarkdownView.Render() unconditionally re-parses the whole Markdown string and rebuilds
// its entire native visual tree -- one Image plus two ContentView wrappers per resolved media link
// -- on every Markdown change. This test pins that mechanism directly (object-identity across an
// edit, not wall-clock timing) rather than trying to reproduce the freeze itself, which needs a
// real device and a large enough biography to be perceptible -- the same reasoning
// ScrollCrashReproTests uses for its own native crash.
//
// The other half of the original finding -- this rebuild ran even while the preview tab was
// hidden, with no gate on visibility at all -- is now fixed (see MarkdownView.OnPropertyChanged /
// Render's IsVisible check) and pinned by MarkdownViewVisibilityGatingTests instead.
public class MarkdownEditorFreezeReproTests
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

  // Reproduces the code-level finding from the issue: an edit that names no media link at all still
  // tears down and reconstructs every already-resolved Image control in the document. This is the
  // wasted work that scales with media-link count regardless of what the edit touched -- N links
  // means N freshly built Image/ContentView/ContentView trees on every keystroke, not just the ones
  // near the edit.
  [Fact]
  public async Task EditingTextThatNamesNoMediaLink_StillRebuildsEveryAlreadyResolvedImage()
  {
    var markdown = MarkdownWithMediaLinks(MediaLinkCount);
    var mediaSources = MediaSources(MediaLinkCount);
    var view = await CreateViewAsync(markdown, mediaSources, MediaLinkCount);

    var before = Descendants(view).OfType<Image>().ToArray();
    Assert.Equal(MediaLinkCount, before.Length);

    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = markdown + "\n\nAn epilogue naming no media link.");

    var after = Descendants(view).OfType<Image>().ToArray();
    Assert.Equal(MediaLinkCount, after.Length);
    Assert.Empty(before.Intersect(after));
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

  private static async Task<MarkdownView> CreateViewAsync(string markdown, IReadOnlyDictionary<string, byte[]> mediaSources, int expectedImages)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var view = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownView
    {
      Markdown = markdown,
      MediaResolver = MakeResolver(mediaSources),
    });

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => Descendants(view).OfType<Image>().Count()),
      rendered => rendered >= expectedImages,
      timeoutMessage: "The resolved media never rendered.");

    return view;
  }

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
