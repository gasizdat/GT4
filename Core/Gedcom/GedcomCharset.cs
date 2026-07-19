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
  /// Detects the encoding to use for <paramref name="stream"/>, reading only its header. A byte-order mark
  /// always wins over the declared <c>CHAR</c> value; an unresolvable value (e.g. <c>ANSI</c>) sets
  /// <see cref="GedcomCharsetResult.NeedsCodepage"/> instead of guessing.
  /// </summary>
  public static async Task<GedcomCharsetResult> DetectAsync(Stream stream, CancellationToken token = default)
  {
    // Latin-1 is a lossless 1-byte-per-char fallback, safe for locating an ASCII header line regardless of
    // what the (still-unknown) body encoding turns out to be. If the stream actually starts with a BOM,
    // StreamReader detects and strips it itself and CurrentEncoding reflects it below -- that is the "BOM
    // wins over declared CHAR" precedence, for free.
    using var reader = new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);

    var declared = await FindDeclaredCharsetAsync(reader, token);

    // CHAR is a required level-1 child of HEAD, and HEAD is always the file's first record, so it cannot
    // legally appear at or after the second level-0 line; FindDeclaredCharsetAsync stops there. By then the
    // first buffer fill (and so the BOM sniff) has always already happened, even on an empty stream.
    if (reader.CurrentEncoding.CodePage != Encoding.Latin1.CodePage)
      return new GedcomCharsetResult(NeedsCodepage: false, reader.CurrentEncoding, DeclaredValue: null);

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

  private static async Task<string?> FindDeclaredCharsetAsync(StreamReader reader, CancellationToken token)
  {
    var topLevelLines = 0;
    string? line;
    while ((line = await reader.ReadLineAsync(token)) is not null)
    {
      var trimmed = line.TrimStart();
      if (trimmed.StartsWith(CharsetLinePrefix, StringComparison.Ordinal))
        return trimmed[CharsetLinePrefix.Length..];

      if (trimmed.StartsWith("0 ", StringComparison.Ordinal) && ++topLevelLines > 1)
        return null;
    }

    return null;
  }
}

/// <summary>
/// The outcome of <see cref="GedcomCharset.DetectAsync"/>: either a resolved <see cref="Encoding"/>, or
/// <see cref="NeedsCodepage"/> set with the raw <see cref="DeclaredValue"/> the file declared, for a caller
/// to resolve (e.g. by asking the user to pick a codepage).
/// </summary>
public readonly record struct GedcomCharsetResult(bool NeedsCodepage, Encoding? Encoding, string? DeclaredValue);
