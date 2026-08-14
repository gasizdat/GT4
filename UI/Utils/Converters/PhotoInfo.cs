namespace GT4.UI.Utils.Converters;

// PixelSize is the size of the bytes Source actually renders, read from their header by the converter
// that holds them; null when they carry no readable one, or when the converter resized them and the
// stored dimensions would misreport what is on screen.
public sealed record PhotoInfo(ImageSource Source, string? Caption, Size? PixelSize = null);
