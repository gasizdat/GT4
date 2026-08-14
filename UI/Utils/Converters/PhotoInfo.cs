namespace GT4.UI.Utils.Converters;

// PixelSize is Source's own dimensions, which only the converter holding its bytes can read; null when
// the converter downsized Source, since a caller laying out by an absolute width has nothing to use.
public sealed record PhotoInfo(ImageSource Source, string? Caption, Size? PixelSize = null);
