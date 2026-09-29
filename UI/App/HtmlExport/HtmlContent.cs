using System.Net;

namespace GT4.UI.HtmlExport;

// Text becomes content only through the implicit conversion, which encodes it; markup this exporter
// wrote or sanitized itself is the only thing that goes in raw.
internal readonly record struct HtmlContent(string Markup)
{
  public static HtmlContent Empty { get; } = new(string.Empty);

  public static implicit operator HtmlContent(string? text) => new(WebUtility.HtmlEncode(text ?? string.Empty));

  public static HtmlContent Raw(string markup) => new(markup);

  public static HtmlContent Join(IEnumerable<HtmlContent> parts)
  {
    var markups = parts.Select(part => part.Markup);
    return new(string.Join('\n', markups));
  }
}
