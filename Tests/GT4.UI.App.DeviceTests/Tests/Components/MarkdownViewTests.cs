using GT4.UI.Components;
using Xunit;

namespace GT4.UI.DeviceTests;

public class MarkdownViewTests
{
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
    var view = await CreateViewAsync("![A caption](media:11)", new Dictionary<int, byte[]> { [11] = [1, 2, 3] });

    var image = Descendants(view).OfType<Image>().Single();

    Assert.IsType<StreamImageSource>(image.Source);
  }

  [Fact]
  public async Task MediaReference_WithNoSuppliedBytes_RendersNoImage()
  {
    var view = await CreateViewAsync("![A caption](media:11)");

    Assert.Empty(Descendants(view).OfType<Image>());
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

  private static Task<MarkdownView> CreateViewAsync(string markdown) =>
    CreateViewAsync(markdown, new Dictionary<int, byte[]>());

  private static async Task<MarkdownView> CreateViewAsync(string markdown, IReadOnlyDictionary<int, byte[]> mediaSources)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new MarkdownView
    {
      Markdown = markdown,
      MediaSources = mediaSources,
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
