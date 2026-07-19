using System.Text;

namespace GT4.Core.Gedcom;

/// <summary>
/// Resolves the text encoding of a GEDCOM file from its declared <c>1 CHAR</c> header line (or a byte-order
/// mark, which takes precedence when present) so import never blindly assumes UTF-8.
/// </summary>
public static class GedcomCharset
{
  private const string CharsetLinePrefix = "1 " + GedcomTags.Charset + " ";

  /// <summary>
  /// Detects the encoding to use for <paramref name="bytes"/>. A byte-order mark always wins; otherwise the
  /// declared <c>CHAR</c> value is consulted. <c>ANSI</c> (and any other unrecognized value) cannot be
  /// resolved to a single codepage from the file alone, so <see cref="GedcomCharsetResult.NeedsCodepage"/> is
  /// set instead of guessing and silently mangling the text. <c>ANSEL</c> is GEDCOM's own 8-bit encoding, not
  /// a Windows codepage picker can offer, and decoding it is unsupported for now.
  /// </summary>
  public static GedcomCharsetResult Detect(byte[] bytes)
  {
    var bom = DetectBom(bytes);
    if (bom is not null)
      return new GedcomCharsetResult(NeedsCodepage: false, bom, DeclaredValue: null);

    var declared = FindDeclaredCharset(bytes);
    return declared?.Trim().ToUpperInvariant() switch
    {
      null => new GedcomCharsetResult(NeedsCodepage: false, Encoding.UTF8, DeclaredValue: null),
      "UTF-8" or "UTF8" => new GedcomCharsetResult(NeedsCodepage: false, Encoding.UTF8, declared),
      "ASCII" => new GedcomCharsetResult(NeedsCodepage: false, Encoding.ASCII, declared),
      "UNICODE" => new GedcomCharsetResult(NeedsCodepage: false, Encoding.Unicode, declared),
      "ANSEL" => throw new NotSupportedException(
        "This GEDCOM file declares the ANSEL charset, which GT4 cannot decode. Re-export it as UTF-8 and import again."),
      _ => new GedcomCharsetResult(NeedsCodepage: true, Encoding: null, declared),
    };
  }

  private static Encoding? DetectBom(byte[] bytes)
  {
    if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
      return Encoding.UTF8;
    if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
      return Encoding.Unicode;
    if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
      return Encoding.BigEndianUnicode;

    return null;
  }

  // CHAR is a required level-1 child of HEAD, and HEAD is always the file's first record, so it cannot
  // legally appear at or after the second level-0 line. That bounds the scan to the header (a handful of
  // short lines) regardless of how large the rest of the file is (e.g. embedded photo BLOBs). Each line is
  // decoded as Latin-1 -- a lossless 1-byte-per-char mapping, safe for locating an ASCII header line
  // regardless of what the (still-unknown) body encoding turns out to be -- one short line at a time rather
  // than materializing the whole file as a string up front.
  private static string? FindDeclaredCharset(byte[] bytes)
  {
    var remaining = bytes.AsSpan();
    var topLevelLines = 0;
    while (!remaining.IsEmpty)
    {
      var newlineIndex = remaining.IndexOf((byte)'\n');
      var lineBytes = newlineIndex < 0 ? remaining : remaining[..newlineIndex];
      remaining = newlineIndex < 0 ? [] : remaining[(newlineIndex + 1)..];

      if (lineBytes.Length > 0 && lineBytes[^1] == (byte)'\r')
        lineBytes = lineBytes[..^1];

      var line = Encoding.Latin1.GetString(lineBytes).TrimStart();
      if (line.StartsWith(CharsetLinePrefix, StringComparison.Ordinal))
        return line[CharsetLinePrefix.Length..];

      if (line.StartsWith("0 ", StringComparison.Ordinal) && ++topLevelLines > 1)
        return null;
    }

    return null;
  }
}

/// <summary>
/// The outcome of <see cref="GedcomCharset.Detect"/>: either a resolved <see cref="Encoding"/>, or
/// <see cref="NeedsCodepage"/> set with the raw <see cref="DeclaredValue"/> the file declared, for a caller
/// to resolve (e.g. by asking the user to pick a codepage).
/// </summary>
public readonly record struct GedcomCharsetResult(bool NeedsCodepage, Encoding? Encoding, string? DeclaredValue);
