namespace GT4.UI.Components;

/// <summary>
/// An image <see cref="MarkdownView"/> inlines for a <c>media:&lt;id&gt;</c> reference. PixelSize is the
/// image's own dimensions, which drive the layout -- it is null only when they could not be read, and
/// such an image lays out fitted to its column rather than at its natural size.
/// </summary>
public sealed record InlineMedia(ImageSource Source, Size? PixelSize);

/// <summary>
/// Resolves a <c>media:&lt;id&gt;</c> reference to the image it names, or null when the id names nothing
/// renderable. The host owns the lookup -- its project transactions, its cancellation, its data
/// converters -- so this view depends on none of them and can inline media it was never handed.
/// </summary>
public delegate Task<InlineMedia?> InlineMediaResolver(int mediaId);
