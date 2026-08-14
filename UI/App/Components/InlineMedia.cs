namespace GT4.UI.Components;

/// <summary>
/// An image <see cref="MarkdownView"/> inlines for a <c>media:&lt;id&gt;</c> reference. PixelSize is the
/// image's own dimensions, which drive the layout -- it is null only when they could not be read, and
/// such an image lays out fitted to its column rather than at its natural size.
/// </summary>
public sealed record InlineMedia(ImageSource Source, Size? PixelSize);

/// <summary>
/// Resolves the link an image in the Markdown points at -- a <c>media:&lt;id&gt;</c> reference or a plain
/// URL -- to the image it names, or null when it names nothing renderable. The whole link is handed over
/// rather than an id parsed out of it, so which schemes exist at all is the host's business: the host
/// owns the lookup (its project transactions, its cancellation, its data converters, its HTTP), and this
/// view depends on none of them.
/// </summary>
public delegate Task<InlineMedia?> InlineMediaResolver(string link);
