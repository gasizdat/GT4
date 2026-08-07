using GT4.UI.Utils;
using Markdig;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Collections.ObjectModel;

namespace GT4.UI.Components;

public class MarkdownView : ContentView
{
  private const string BodyTextSizeKey = "LabelTextSizeDefault";
  private const string BlockSpacingKey = "PageContentSpacing";
  private const double MarkerSpacing = 6;
  private const double QuoteBarWidth = 3;
  private const double CodePadding = 8;
  private const double RuleHeight = 1;

  // Indexed by heading level; the app's scale bottoms out at the body size, so H5 and H6 share it.
  private static readonly string[] HeadingTextSizeKeys =
  [
    "LabelTextSizeGiant",
    "LabelTextSizeHuge",
    "LabelTextSizeLarge",
    "LabelTextSizeMedium",
    BodyTextSizeKey,
    BodyTextSizeKey,
  ];

  private static readonly MarkdownPipeline Pipeline = BuildPipeline();

  private readonly Command<string> _LinkCommand;

  public MarkdownView()
  {
    _LinkCommand = new Command<string>(OnLinkTapped);
  }

  public static readonly BindableProperty MarkdownProperty =
    BindableProperty.Create(nameof(Markdown), typeof(string), typeof(MarkdownView), default, BindingMode.OneWay, null, OnSourceChanged);

  // Host-supplied id-to-bytes map for "![caption](media:id)" references; keeps this view free of any
  // project dependency (the host already resolves photo bytes for its own display, e.g. PersonPage).
  public static readonly BindableProperty MediaSourcesProperty =
    BindableProperty.Create(nameof(MediaSources), typeof(IReadOnlyDictionary<int, byte[]>), typeof(MarkdownView),
      ReadOnlyDictionary<int, byte[]>.Empty, BindingMode.OneWay, null, OnSourceChanged);

  public string? Markdown
  {
    get => (string?)GetValue(MarkdownProperty);
    set => SetValue(MarkdownProperty, value);
  }

  public IReadOnlyDictionary<int, byte[]> MediaSources
  {
    get => (IReadOnlyDictionary<int, byte[]>)GetValue(MediaSourcesProperty);
    set => SetValue(MediaSourcesProperty, value);
  }

  // Raised instead of navigating when a rendered [Name](person:123) link is tapped; the host page owns
  // person lookup/navigation, so this view stays free of any project or navigation dependency.
  public event EventHandler<int>? PersonLinkTapped;

  // Raised instead of navigating when a rendered [Name](attachment:123) link is tapped; the host page owns
  // opening the referenced attachment, same reason as PersonLinkTapped.
  public event EventHandler<int>? AttachmentLinkTapped;

  // CommonMark hands a run of lines opening with a tag to the HTML block parser as one opaque chunk,
  // and this renderer has no shape for one -- the text inside would render as nothing at all. Without
  // that parser the tags parse as inline HTML, which the inline walker already drops while keeping the
  // text they wrap.
  private static MarkdownPipeline BuildPipeline()
  {
    var builder = new MarkdownPipelineBuilder()
      .UseAdvancedExtensions()
      .UseSoftlineBreakAsHardlineBreak();
    builder.BlockParsers.TryRemove<HtmlBlockParser>();
    return builder.Build();
  }

  private static void OnSourceChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is MarkdownView view && oldValue != newValue)
    {
      view.Render();
    }
  }

  // "person:"/"attachment:"/"media:" are custom opaque schemes (like "mailto:"); Uri's hierarchical-URI
  // members (Host, AbsolutePath) aren't reliable for them, so the id is taken directly from the string.
  private static bool TryParseLink(string url, string prefix, out int id)
  {
    id = 0;
    return url.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(url.AsSpan(prefix.Length), out id);
  }

  private static Color ResourceColor(string key) => (Color)Application.Current!.Resources[key];

  private static void SetThemeColor(BindableObject target, BindableProperty property, string lightKey, string darkKey)
  {
    var light = ResourceColor(lightKey);
    var dark = ResourceColor(darkKey);
    target.SetAppThemeColor(property, light, dark);
  }

  private static VerticalStackLayout CreateBlockStack()
  {
    var stack = new VerticalStackLayout();
    stack.SetDynamicResource(StackBase.SpacingProperty, BlockSpacingKey);
    return stack;
  }

  private static Span CreateSpan(string text, InlineStyle style)
  {
    var span = new Span
    {
      Text = text,
      FontAttributes = style.Attributes,
      TextDecorations = style.Decorations,
    };
    span.SetDynamicResource(Span.FontSizeProperty, style.TextSizeKey);

    if (style.IsLink)
    {
      SetThemeColor(span, Span.TextColorProperty, "Accent", "AccentDark");
    }

    return span;
  }

  // "++text++" is Pandoc insert syntax, which the advanced extensions parse like any other emphasis.
  private static InlineStyle ApplyEmphasis(InlineStyle style, EmphasisInline emphasis) =>
    emphasis switch
    {
      { DelimiterCount: 2, DelimiterChar: '~' } => style with { Decorations = style.Decorations | TextDecorations.Strikethrough },
      { DelimiterCount: 2, DelimiterChar: '+' } => style with { Decorations = style.Decorations | TextDecorations.Underline },
      { DelimiterCount: 2 } => style with { Attributes = style.Attributes | FontAttributes.Bold },
      _ => style with { Attributes = style.Attributes | FontAttributes.Italic },
    };

  private static View RenderCode(CodeBlock code)
  {
    var label = new Label { Text = code.Lines.ToString() };
    label.SetDynamicResource(Label.FontSizeProperty, BodyTextSizeKey);

    var border = new Border
    {
      Content = label,
      Padding = CodePadding,
      StrokeThickness = 0,
    };
    SetThemeColor(border, BackgroundColorProperty, "SurfaceSubtle", "SurfaceSubtleDark");
    return border;
  }

  private static View RenderThematicBreak()
  {
    var rule = new BoxView { HeightRequest = RuleHeight };
    SetThemeColor(rule, BoxView.ColorProperty, "Gray200", "Gray500");
    return rule;
  }

  private static (int? WidthPercent, string Caption) DescriptionOf(LinkInline image)
  {
    var literals = image.OfType<LiteralInline>().Select(literal => literal.Content.ToString());
    var description = string.Concat(literals);
    return MediaLinkUtils.ParseImageDescription(description);
  }

  // MAUI keeps the height it measured a full-width image at, so capping the width alone leaves it in an
  // over-tall frame: both axes have to come from the host's width. That needs the pixel dimensions, so a
  // remote image -- whose bytes this view doesn't hold -- keeps the plain fit, percentage included.
  private static View ScaledImage(ImageSource source, Size? pixelSize, int? widthPercent)
  {
    var image = new Image { Source = source, Aspect = Aspect.AspectFit };
    if (pixelSize is null)
    {
      return image;
    }

    image.HorizontalOptions = LayoutOptions.Start;
    var host = new ContentView { Content = image };
    host.SizeChanged += (_, _) => ScaleToHost(host, image, pixelSize.Value, widthPercent);
    // Apply sizing immediately in case the host already has a measured width (avoids race where the
    // SizeChanged handler runs later and the test reads the image size before scaling).
    if (host.Width > 0)
    {
      ScaleToHost(host, image, pixelSize.Value, widthPercent);
    }
    return host;
  }

  // Unasked, an image lays its pixel count out as device-independent units and shrinks only to fit the
  // column; a percentage is that share of the same width, so it enlarges only when asked for over 100%.
  private static void ScaleToHost(ContentView host, Image image, Size pixelSize, int? widthPercent)
  {
    if (host.Width <= 0)
    {
      return;
    }

    var fitWidth = Math.Min(pixelSize.Width, host.Width);
    // Percentages are a share of the constraining width (pixel size or host column). This keeps the
    // image from growing beyond the column when the host is narrow, while still allowing enlargement
    // when the host provides room.
    var width = widthPercent is null
      ? fitWidth
      : fitWidth * widthPercent.Value / 100.0;

    image.WidthRequest = width;
    image.HeightRequest = width * pixelSize.Height / pixelSize.Width;
  }

  private void Render()
  {
    var document = Markdig.Markdown.Parse(Markdown ?? string.Empty, Pipeline);
    Content = RenderContainer(document);
  }

  private View RenderContainer(ContainerBlock container)
  {
    var stack = CreateBlockStack();
    foreach (var block in container)
    {
      var view = RenderBlock(block);
      if (view is not null)
      {
        stack.Add(view);
      }
    }

    return stack;
  }

  // The container case is the fallback: a block with no shape of its own here (a table, a footnote
  // group) still renders the text its children hold.
  private View? RenderBlock(Block block) => block switch
  {
    HeadingBlock heading => RenderText(heading, HeadingTextSizeKeys[heading.Level - 1], FontAttributes.Bold),
    ParagraphBlock paragraph => RenderText(paragraph, BodyTextSizeKey, FontAttributes.None),
    ListBlock list => RenderList(list),
    QuoteBlock quote => RenderQuote(quote),
    CodeBlock code => RenderCode(code),
    ThematicBreakBlock => RenderThematicBreak(),
    ContainerBlock container => RenderContainer(container),
    _ => null,
  };

  // An image can't live in a FormattedString, so a block carrying one splits into alternating labels
  // and images.
  private View RenderText(LeafBlock block, string textSizeKey, FontAttributes attributes)
  {
    var style = new InlineStyle(textSizeKey, attributes, TextDecorations.None, IsLink: false);
    var views = new List<View>();
    var formatted = new FormattedString();

    foreach (var inline in block.Inline!)
    {
      if (inline is not LinkInline { IsImage: true } image)
      {
        RenderInline(formatted, inline, style);
        continue;
      }

      if (formatted.Spans.Count > 0)
      {
        views.Add(new Label { FormattedText = formatted });
        formatted = new FormattedString();
      }

      var description = DescriptionOf(image);
      var imageView = CreateImageView(image.Url, description.WidthPercent);
      if (imageView is not null)
      {
        views.Add(imageView);
      }
    }

    if (formatted.Spans.Count > 0)
    {
      views.Add(new Label { FormattedText = formatted });
    }

    if (views.Count == 1)
    {
      return views[0];
    }

    var stack = CreateBlockStack();
    foreach (var view in views)
    {
      stack.Add(view);
    }

    return stack;
  }

  private void RenderInline(FormattedString formatted, Inline inline, InlineStyle style)
  {
    switch (inline)
    {
      case LiteralInline literal:
        formatted.Spans.Add(CreateSpan(literal.Content.ToString(), style));
        break;

      case EmphasisInline emphasis:
        var emphasized = ApplyEmphasis(style, emphasis);
        foreach (var child in emphasis)
        {
          RenderInline(formatted, child, emphasized);
        }
        break;

      case CodeInline code:
        var codeSpan = CreateSpan(code.Content, style);
        SetThemeColor(codeSpan, Span.BackgroundColorProperty, "SurfaceSubtle", "SurfaceSubtleDark");
        formatted.Spans.Add(codeSpan);
        break;

      case LineBreakInline:
        formatted.Spans.Add(CreateSpan("\n", style));
        break;

      // An image nested in emphasis or in a link can't become an Image view from inside a FormattedString,
      // so its caption stands in -- minus the size token, which is markup rather than text.
      case LinkInline { IsImage: true } image:
        var (_, caption) = DescriptionOf(image);
        formatted.Spans.Add(CreateSpan(caption, style));
        break;

      case LinkInline link:
        RenderLink(formatted, link, style);
        break;

      // A hand-typed HTML tag has no span of its own and so disappears, while the text it wraps keeps
      // rendering through this case.
      case ContainerInline container:
        foreach (var child in container)
        {
          RenderInline(formatted, child, style);
        }
        break;
    }
  }

  private void RenderLink(FormattedString formatted, LinkInline link, InlineStyle style)
  {
    var linkStyle = style with { Decorations = style.Decorations | TextDecorations.Underline, IsLink = true };
    var first = formatted.Spans.Count;
    foreach (var child in link)
    {
      RenderInline(formatted, child, linkStyle);
    }

    var url = link.Url ?? string.Empty;
    var spans = formatted.Spans.Skip(first);
    foreach (var span in spans)
    {
      span.GestureRecognizers.Add(new TapGestureRecognizer { Command = _LinkCommand, CommandParameter = url });
    }
  }

  private View RenderList(ListBlock list)
  {
    var stack = CreateBlockStack();
    var number = list.OrderedStart is null ? 1 : int.Parse(list.OrderedStart);
    foreach (var item in list)
    {
      var marker = list.IsOrdered ? $"{number++}{list.OrderedDelimiter}" : "•";
      stack.Add(RenderListItem(marker, (ListItemBlock)item));
    }

    return stack;
  }

  private View RenderListItem(string marker, ListItemBlock item)
  {
    var markerLabel = new Label { Text = marker };
    markerLabel.SetDynamicResource(Label.FontSizeProperty, BodyTextSizeKey);

    var row = CreateMarkedRow(markerLabel);
    row.Add(RenderContainer(item), 1);
    return row;
  }

  private View RenderQuote(QuoteBlock quote)
  {
    var bar = new BoxView { WidthRequest = QuoteBarWidth };
    SetThemeColor(bar, BoxView.ColorProperty, "Gray200", "Gray500");

    var row = CreateMarkedRow(bar);
    row.Add(RenderContainer(quote), 1);
    return row;
  }

  private static Grid CreateMarkedRow(View marker)
  {
    var row = new Grid
    {
      ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
      ColumnSpacing = MarkerSpacing,
    };
    row.Add(marker);
    return row;
  }

  // An id with no entry (a dangling reference, or one belonging to another person) renders nothing
  // rather than a broken image.
  private View? CreateImageView(string? url, int? widthPercent)
  {
    if (url is null)
    {
      return null;
    }

    if (TryParseLink(url, "media:", out var mediaId))
    {
      if (!MediaSources.TryGetValue(mediaId, out var bytes))
      {
        return null;
      }

      var stored = ImageSource.FromStream(() => new MemoryStream(bytes));
      return ScaledImage(stored, ImageUtils.PixelSize(bytes), widthPercent);
    }

    return Uri.TryCreate(url, UriKind.Absolute, out var uri)
      ? ScaledImage(ImageSource.FromUri(uri), null, widthPercent)
      : null;
  }

  // A person or attachment link is handled in-app through its event; anything else (the Google Maps
  // coordinate reference, a mailto/tel in a bio) is handed to the OS by its own protocol.
  private async void OnLinkTapped(string url)
  {
    if (TryParseLink(url, "person:", out var personId))
    {
      PersonLinkTapped?.Invoke(this, personId);
      return;
    }

    if (TryParseLink(url, "attachment:", out var attachmentId))
    {
      AttachmentLinkTapped?.Invoke(this, attachmentId);
      return;
    }

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
      return;
    }

    try
    {
      await Launcher.Default.OpenAsync(uri);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  // Spans carry their own font and colour -- none of it is inherited from the hosting Label -- so the
  // block-level choices have to travel down the inline tree.
  private readonly record struct InlineStyle(string TextSizeKey, FontAttributes Attributes, TextDecorations Decorations, bool IsLink);
}
