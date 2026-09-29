using GT4.UI.Utils;
using Markdig;
using Markdig.Parsers;
using Markdig.Syntax.Inlines;

namespace GT4.UI.Components;

public static class BiographyMarkdown
{
  public static readonly MarkdownPipeline Pipeline = BuildPipeline();

  public static (int? WidthPercent, string Caption) DescriptionOf(LinkInline image)
  {
    var literals = image.OfType<LiteralInline>().Select(literal => literal.Content.ToString());
    var description = string.Concat(literals);
    return MarkdownLinkUtils.ParseImageDescription(description);
  }

  // CommonMark hands a run of lines opening with a tag to the HTML block parser as one opaque chunk,
  // and MarkdownView has no shape for one -- the text inside would render as nothing at all. Without
  // that parser the tags parse as inline HTML, which MarkdownView's inline walker already drops while
  // keeping the text they wrap.
  private static MarkdownPipeline BuildPipeline()
  {
    var builder = new MarkdownPipelineBuilder()
      .UseAdvancedExtensions()
      .UseSoftlineBreakAsHardlineBreak();
    builder.BlockParsers.TryRemove<HtmlBlockParser>();
    return builder.Build();
  }
}
