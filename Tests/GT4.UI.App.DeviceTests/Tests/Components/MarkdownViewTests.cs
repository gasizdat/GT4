using GT4.Core.Project.Abstraction;
using GT4.UI.Components;
using GT4.UI.Utils;
using Xunit;

namespace GT4.UI.DeviceTests;

public class MarkdownViewTests
{
  private const double SamplePngWidth = 40;
  private const double SamplePngHeight = 20;

  // A 40x20 PNG: the sizing tests need real bytes, since the pixel dimensions drive the layout.
  private static readonly byte[] SamplePng =
  [
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x14, 0x08, 0x06, 0x00, 0x00, 0x00, 0xFF, 0x46, 0x7F,
    0xBB, 0x00, 0x00, 0x00, 0x01, 0x73, 0x52, 0x47, 0x42, 0x00, 0xAE, 0xCE, 0x1C, 0xE9, 0x00, 0x00,
    0x00, 0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00, 0xB1, 0x8F, 0x0B, 0xFC, 0x61, 0x05, 0x00, 0x00,
    0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01, 0xC7,
    0x6F, 0xA8, 0x64, 0x00, 0x00, 0x00, 0x35, 0x49, 0x44, 0x41, 0x54, 0x48, 0x4B, 0xED, 0xCE, 0xA1,
    0x01, 0x00, 0x00, 0x08, 0x80, 0x30, 0x8F, 0xF5, 0x25, 0xCF, 0xF4, 0x06, 0xED, 0x56, 0x8A, 0x81,
    0xB0, 0x42, 0x22, 0xB2, 0x7A, 0x3E, 0x8B, 0x1B, 0xBE, 0x71, 0x90, 0x72, 0x90, 0x72, 0x90, 0x72,
    0x90, 0x72, 0x90, 0x72, 0x90, 0x72, 0x90, 0x72, 0x90, 0x72, 0x90, 0x5A, 0x1E, 0x11, 0x0C, 0x28,
    0xBD, 0x49, 0x70, 0x61, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
  ];

  [Fact]
  public async Task PersonLink_RaisesPersonLinkTappedWithItsId()
  {
    var view = await CreateViewAsync("See [Jane Doe](person:123) for details.");
    var tapped = new List<int>();
    view.PersonLinkTapped += (_, id) => tapped.Add(id);

    var link = SpanWithText(view, "Jane Doe");
    Tap(link);

    Assert.Equal([123], tapped);
  }

  [Fact]
  public async Task AttachmentLink_RaisesAttachmentLinkTappedWithItsId()
  {
    var view = await CreateViewAsync("See [scan.pdf](attachment:20) for details.");
    var tapped = new List<int>();
    view.AttachmentLinkTapped += (_, id) => tapped.Add(id);

    var link = SpanWithText(view, "scan.pdf");
    Tap(link);

    Assert.Equal([20], tapped);
  }

  [Fact]
  public async Task MediaReference_RendersAnImageFromTheSuppliedBytes()
  {
    var view = await CreateViewAsync("![A caption](media:11)", new Dictionary<string, byte[]> { ["media:11"] = [1, 2, 3] });

    var image = Descendants(view).OfType<Image>().Single();

    Assert.IsType<StreamImageSource>(image.Source);
  }

  [Fact]
  public async Task MediaReference_SmallerThanItsColumn_KeepsItsPixelSize()
  {
    await AssertRenderedImageSizeAsync(
      "![A caption](media:11)",
      columnWidth: 300,
      new Size(SamplePngWidth, SamplePngHeight));
  }

  // Capping the width alone clips the image horizontally and leaves it floating in a frame as tall as
  // the full-width one would have been.
  [Fact]
  public async Task MediaReference_WiderThanItsColumn_ShrinksKeepingItsAspectRatio()
  {
    await AssertRenderedImageSizeAsync(
      "![A caption](media:11)",
      columnWidth: 20,
      new Size(20, 10));
  }

  [Fact]
  public async Task MediaReference_WithATrailingPercentage_TakesThatShareOfItsPixelSize()
  {
    await AssertRenderedImageSizeAsync(
      "![A caption 50%](media:11)",
      columnWidth: 300,
      new Size(SamplePngWidth / 2, SamplePngHeight / 2));
  }

  [Fact]
  public async Task MediaReference_WiderThanItsColumn_TakesItsPercentageOfTheColumn()
  {
    await AssertRenderedImageSizeAsync(
      "![A caption 50%](media:11)",
      columnWidth: 20,
      new Size(10, 5));
  }

  [Fact]
  public async Task MediaReference_WithAPercentageOverAHundred_GrowsPastItsPixelSize()
  {
    await AssertRenderedImageSizeAsync(
      "![A caption 150%](media:11)",
      columnWidth: 300,
      new Size(SamplePngWidth * 1.5, SamplePngHeight * 1.5));
  }

  // The overflow is intentional, not a missed clamp -- a real biography card clips whatever spills past it.
  [Fact]
  public async Task MediaReference_EnlargedPastItsColumn_OverflowsRatherThanClamping()
  {
    await AssertRenderedImageSizeAsync(
      "![A caption 150%](media:11)",
      columnWidth: 20,
      new Size(30, 15));
  }

  // A URL image scales like any other, which it could not while the view built its ImageSource itself:
  // it had no dimensions for one, so the percentage was parsed, stripped from the caption and dropped.
  [Fact]
  public async Task RemoteImage_WithATrailingPercentage_TakesThatShareOfItsPixelSize()
  {
    await AssertRenderedImageSizeAsync(
      "![A caption 50%](https://example.test/photo.png)",
      columnWidth: 300,
      new Size(SamplePngWidth / 2, SamplePngHeight / 2),
      link: "https://example.test/photo.png");
  }

  // The nested-image path renders the caption as text, so the size token has to be stripped there or
  // it would show up on screen as "Grandpa 50%".
  [Fact]
  public async Task ImageInsideEmphasis_RendersItsCaptionWithoutTheSizeTokenOrALink()
  {
    var view = await CreateViewAsync("Look: *![Grandpa 50%](media:11)* here.", new Dictionary<string, byte[]> { ["media:11"] = [1, 2, 3] }, expectedImages: 0);

    var caption = SpanWithText(view, "Grandpa");

    Assert.Equal(FontAttributes.Italic, caption.FontAttributes);
    Assert.Empty(caption.GestureRecognizers);
  }

  [Fact]
  public async Task ImageUsedAsALinkLabel_TapsOnlyTheEnclosingLink()
  {
    var view = await CreateViewAsync("[![cap](media:11)](person:5)", new Dictionary<string, byte[]> { ["media:11"] = [1, 2, 3] }, expectedImages: 0);
    var tapped = new List<int>();
    view.PersonLinkTapped += (_, id) => tapped.Add(id);

    var label = SpanWithText(view, "cap");
    var recognizer = Assert.Single(label.GestureRecognizers);
    Tap(label);

    Assert.Equal("person:5", ((TapGestureRecognizer)recognizer).CommandParameter);
    Assert.Equal([5], tapped);
  }

  // A cancelled lookup says nothing about the id -- the host's short-operation token can outrun a slow
  // remote image -- so it must not be remembered as "this renders nothing".
  [Fact]
  public async Task MediaReference_ResolverCancelled_IsAskedAgainOnTheNextEdit()
  {
    var attempts = 0;
    var view = await CreateViewAsync("![a](media:11)", mediaId =>
    {
      attempts++;
      return attempts == 1
        ? throw new OperationCanceledException()
        : Task.FromResult<InlineMedia?>(new InlineMedia(ImageSource.FromStream(() => new MemoryStream(SamplePng)), null));
    });

    await Poll.UntilAsync(() => Task.FromResult(attempts), tried => tried >= 1, timeoutMessage: "The resolver was never called.");
    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = "![a](media:11) and more");

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => Descendants(view).OfType<Image>().Count()),
      rendered => rendered == 1,
      timeoutMessage: "The cancelled media was never retried.");
  }

  // Same rule, other half: the project closing underneath a resolve in flight (Android backgrounding)
  // is a fact about the app's lifecycle, not about the id.
  [Fact]
  public async Task MediaReference_ResolverHitTheProjectTeardown_IsAskedAgainOnTheNextEdit()
  {
    var attempts = 0;
    var view = await CreateViewAsync("![a](media:11)", mediaId =>
    {
      attempts++;
      return attempts == 1
        ? throw new ProjectNotOpenedException()
        : Task.FromResult<InlineMedia?>(new InlineMedia(ImageSource.FromStream(() => new MemoryStream(SamplePng)), null));
    });

    await Poll.UntilAsync(() => Task.FromResult(attempts), tried => tried >= 1, timeoutMessage: "The resolver was never called.");
    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = "![a](media:11) and more");

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => Descendants(view).OfType<Image>().Count()),
      rendered => rendered == 1,
      timeoutMessage: "The media lost to the teardown was never retried.");
  }

  // The counterpart: an id the resolver answered for is settled, so editing must not re-query it.
  [Fact]
  public async Task MediaReference_ResolvedToNothing_IsNotAskedAgain()
  {
    var attempts = 0;
    var view = await CreateViewAsync("![a](media:11)", mediaId =>
    {
      attempts++;
      return Task.FromResult<InlineMedia?>(null);
    });

    await Poll.UntilAsync(() => Task.FromResult(attempts), tried => tried >= 1, timeoutMessage: "The resolver was never called.");
    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = "![a](media:11) and more");
    await MainThread.InvokeOnMainThreadAsync(() => { });

    Assert.Equal(1, attempts);
  }

  // Every keystroke in the biography editor re-renders the preview, and each lookup is a round-trip
  // through the project's single gated connection -- so an id already in flight must not be reissued.
  [Fact]
  public async Task MediaReference_EditedWhileResolving_IsAskedOnlyOnce()
  {
    var attempts = 0;
    var release = new TaskCompletionSource();
    var view = await CreateViewAsync("![a](media:11)", async mediaId =>
    {
      attempts++;
      await release.Task;
      return new InlineMedia(ImageSource.FromStream(() => new MemoryStream(SamplePng)), null);
    });

    await Poll.UntilAsync(() => Task.FromResult(attempts), tried => tried >= 1, timeoutMessage: "The resolver was never called.");
    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = "![a](media:11) plus");
    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = "![a](media:11) plus more");
    release.SetResult();

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => Descendants(view).OfType<Image>().Count()),
      rendered => rendered == 1,
      timeoutMessage: "The resolved media never rendered.");
    Assert.Equal(1, attempts);
  }

  // The memo belongs to the resolver that filled it. The edit to text naming no media is what makes
  // this bite: the refresh that follows the swap has nothing to resolve, so only the swap itself can
  // fence off the batch the replaced resolver is still running.
  [Fact]
  public async Task MediaReference_ResolverReplacedWhileResolving_DiscardsTheReplacedOnesAnswer()
  {
    var release = new TaskCompletionSource();
    var replacedAnswered = false;
    var replacedSource = ImageSource.FromStream(() => new MemoryStream(SamplePng));
    var currentSource = ImageSource.FromStream(() => new MemoryStream(SamplePng));
    var view = await CreateViewAsync("![a](media:11)", async mediaId =>
    {
      await release.Task;
      replacedAnswered = true;
      return new InlineMedia(replacedSource, null);
    });

    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = "Nothing inlined here.");
    await MainThread.InvokeOnMainThreadAsync(() =>
      view.MediaResolver = mediaId => Task.FromResult<InlineMedia?>(new InlineMedia(currentSource, null)));

    release.SetResult();
    await Poll.UntilAsync(() => Task.FromResult(replacedAnswered), answered => answered, timeoutMessage: "The replaced resolver never answered.");

    // The discarded batch applies on the main thread, so let a few dispatcher turns pass -- reading
    // before it lands would pass even with its answer still being memoised.
    for (var turn = 0; turn < 5; turn++)
    {
      await MainThread.InvokeOnMainThreadAsync(() => { });
    }

    await MainThread.InvokeOnMainThreadAsync(() => view.Markdown = "![a](media:11)");
    var image = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => Descendants(view).OfType<Image>().SingleOrDefault()),
      rendered => rendered is not null,
      timeoutMessage: "The media was never rendered again.");

    Assert.Same(currentSource, image!.Source);
  }

  [Fact]
  public async Task InlineCode_IsGivenTheCodeBackground()
  {
    var view = await CreateViewAsync("Run `dotnet test` now.");

    var code = SpanWithText(view, "dotnet test");

    Assert.NotNull(code.BackgroundColor);
  }

  [Fact]
  public async Task SoftLineBreak_IsRenderedAsASingleNewline()
  {
    var view = await CreateViewAsync("Line one\nLine two");

    var texts = Descendants(view).OfType<Span>().Select(span => span.Text);

    Assert.Equal(["Line one", "\n", "Line two"], texts);
  }

  // The second reference is what makes this a test: the view renders its text before the resolver
  // answers, so asserting on the first pass would pass even with resolution completely broken. Waiting
  // for the resolvable image proves the unresolvable one was asked for and declined.
  [Fact]
  public async Task MediaReference_TheResolverDeclines_RendersNoImageForIt()
  {
    var view = await CreateViewAsync(
      "![resolved](media:11) ![missing](media:12)",
      new Dictionary<string, byte[]> { ["media:11"] = SamplePng });

    var image = Descendants(view).OfType<Image>().Single();

    Assert.IsType<StreamImageSource>(image.Source);
  }

  [Fact]
  public async Task Emphasis_IsRenderedAsTheMatchingFontAttribute()
  {
    var view = await CreateViewAsync("Plain, *italic* and **bold**.");

    Assert.Equal(FontAttributes.Italic, SpanWithText(view, "italic").FontAttributes);
    Assert.Equal(FontAttributes.Bold, SpanWithText(view, "bold").FontAttributes);
  }

  [Fact]
  public async Task StrikethroughAndInsert_AreRenderedAsTheMatchingDecoration()
  {
    var view = await CreateViewAsync("A ~~struck~~ and an ++inserted++ word.");

    Assert.Equal(TextDecorations.Strikethrough, SpanWithText(view, "struck").TextDecorations);
    Assert.Equal(TextDecorations.Underline, SpanWithText(view, "inserted").TextDecorations);
  }

  [Fact]
  public async Task HandTypedHtmlTags_AreDroppedButTheirTextSurvives()
  {
    var view = await CreateViewAsync("A <u>marked</u> word.");

    var texts = Descendants(view).OfType<Span>().Select(span => span.Text);

    Assert.Equal(["A ", "marked", " word."], texts);
  }

  // A tag on its own line is CommonMark's HTML-block shape, distinct from the inline one above.
  [Fact]
  public async Task HandTypedHtmlBlocks_AreDroppedButTheirTextSurvives()
  {
    var view = await CreateViewAsync("<div>\nSome text\n</div>");

    var texts = Descendants(view).OfType<Span>().Select(span => span.Text);

    Assert.Equal(["\n", "Some text", "\n"], texts);
  }

  [Fact]
  public async Task TableCells_KeepTheirTextThroughTheContainerFallback()
  {
    var view = await CreateViewAsync("| Year | Place |\n|---|---|\n| 1902 | Vienna |");

    var texts = Descendants(view).OfType<Span>().Select(span => span.Text);

    Assert.Equal(["Year", "Place", "1902", "Vienna"], texts);
  }

  // Every block shape resolves its colours by resource key, so rendering one is what proves the key
  // is still in the palette -- a renamed key surfaces here rather than as a throw on a real biography.
  [Fact]
  public async Task Quote_CodeBlock_AndThematicBreak_RenderTheirOwnShapes()
  {
    var view = await CreateViewAsync("> Quoted\n\n```\ncode line\n```\n\n---\n");

    var descendants = Descendants(view).ToList();

    Assert.Contains(descendants.OfType<Label>(), label => label.Text?.Trim() == "code line");
    Assert.Contains(descendants.OfType<Span>(), span => span.Text == "Quoted");
    Assert.Equal(2, descendants.OfType<BoxView>().Count());
  }

  [Fact]
  public async Task Heading_IsRenderedBoldAndLargerThanBodyText()
  {
    var view = await CreateViewAsync("# A life\n\nSome prose.");

    var heading = SpanWithText(view, "A life");
    var body = SpanWithText(view, "Some prose.");

    Assert.Equal(FontAttributes.Bold, heading.FontAttributes);
    Assert.True(heading.FontSize > body.FontSize);
  }

  [Fact]
  public async Task ListItem_KeepsItsTextAndGainsAMarker()
  {
    var view = await CreateViewAsync("- First\n- Second");

    var texts = Descendants(view).OfType<Span>().Select(span => span.Text);

    Assert.Equal(["First", "Second"], texts);
    Assert.Equal(2, Descendants(view).OfType<Label>().Count(label => label.Text == "•"));
  }

  // Lays the view out inside a fixed-width column on a real window, so the assertions are about the
  // size the image ends up drawn at rather than the layout properties asked for.
  private static async Task AssertRenderedImageSizeAsync(string markdown, double columnWidth, Size expectedSize, string link = "media:11")
  {
    var view = await CreateViewAsync(markdown, new Dictionary<string, byte[]> { [link] = SamplePng });
    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage
    {
      Content = new VerticalStackLayout
      {
        WidthRequest = columnWidth,
        HorizontalOptions = LayoutOptions.Start,
        Children = { view },
      },
    });

    await using var attachment = await WindowHost.AttachAsync(page);
    var image = Descendants(view).OfType<Image>().Single();

    // The platform snaps a requested DP size up to a whole device pixel before arranging it, so on a
    // density that doesn't divide evenly (e.g. 2.75x) the arranged size can land up to one device pixel
    // above what was asked for -- a real difference, not a settling race, so it never closes by polling.
    // Only one dimension is snapped and the aspect ratio then carries that error into the other, scaled:
    // a 2:1 image snapped a pixel taller comes out two pixels wider, which one flat pixel would reject.
    var density = await MainThread.InvokeOnMainThreadAsync(() => DeviceDisplay.MainDisplayInfo.Density);
    var aspect = expectedSize.Width / expectedSize.Height;
    var tolerance = new Size(Math.Max(1, aspect) / density, Math.Max(1, 1 / aspect) / density);
    var observed = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => new Size(image.Width, image.Height)),
      size => size.Width > 0 && size.Height > 0,
      timeoutMessage: "The image was never laid out.");

    for (var attempt = 0; attempt < 50 && !IsCloseTo(observed, expectedSize, tolerance); attempt++)
    {
      await Task.Delay(20);
      observed = await MainThread.InvokeOnMainThreadAsync(() => new Size(image.Width, image.Height));
    }

    // A stuck arrange reports the requested size and the arranged one separately, which is the only way
    // to tell WinUI ignoring a correct request apart from this code computing a wrong one.
    var diag = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var host = image.Parent as VisualElement;
      return $"WR={image.WidthRequest} HR={image.HeightRequest} bounds={image.Bounds} " +
             $"host={host?.Width}x{host?.Height} density={density}";
    });

    Assert.True(
      IsCloseTo(observed, expectedSize, tolerance),
      $"Expected a size within {tolerance.Width:F3}x{tolerance.Height:F3} of {expectedSize}, but observed {observed}. DIAG {diag}");
  }

  private static bool IsCloseTo(Size observed, Size expected, Size tolerance) =>
    Math.Abs(observed.Width - expected.Width) <= tolerance.Width &&
    Math.Abs(observed.Height - expected.Height) <= tolerance.Height;

  private static Task<MarkdownView> CreateViewAsync(string markdown) =>
    CreateViewAsync(markdown, new Dictionary<string, byte[]>(), expectedImages: 0);

  // Stands in for a host's resolver, keyed by the whole link the way the real one is and measuring the
  // same way InlineMediaProvider does, so the layout assertions run on real pixel dimensions read from
  // real bytes -- for a URL exactly as for a "media:" id.
  private static async Task<MarkdownView> CreateViewAsync(
    string markdown, IReadOnlyDictionary<string, byte[]> mediaSources, int expectedImages = 1)
  {
    InlineMediaResolver resolver = link =>
    {
      if (!mediaSources.TryGetValue(link, out var bytes))
      {
        return Task.FromResult<InlineMedia?>(null);
      }

      var source = ImageSource.FromStream(() => new MemoryStream(bytes));
      var pixelSize = ImageUtils.PixelSize(bytes);
      return Task.FromResult<InlineMedia?>(new InlineMedia(source, pixelSize));
    };

    var view = await CreateViewAsync(markdown, resolver);

    // The view renders its text first and re-renders once the resolver answers, so anything asserting on
    // a rendered image has to wait for that second pass rather than read the first one. Nested media
    // (inside emphasis, or as a link label) renders as its caption instead, hence expectedImages: 0.
    if (expectedImages > 0)
    {
      await Poll.UntilAsync(
        () => MainThread.InvokeOnMainThreadAsync(() => Descendants(view).OfType<Image>().Count()),
        rendered => rendered >= expectedImages,
        timeoutMessage: "The resolved media never rendered.");
    }

    return view;
  }

  private static async Task<MarkdownView> CreateViewAsync(string markdown, InlineMediaResolver resolver)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new MarkdownView
    {
      Markdown = markdown,
      MediaResolver = resolver,
    });
  }

  private static Span SpanWithText(MarkdownView view, string text)
  {
    var spans = Descendants(view).OfType<Span>();
    return spans.Single(span => span.Text == text);
  }

  private static void Tap(Span span)
  {
    var tap = span.GestureRecognizers.OfType<TapGestureRecognizer>().Single();
    tap.Command!.Execute(tap.CommandParameter);
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
