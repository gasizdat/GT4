using System.Text.RegularExpressions;

namespace GT4.UI.HtmlExport;

// A fragment under HtmlExport/Templates whose {{slot}} placeholders are filled with content.
internal sealed partial class HtmlTemplate
{
  private const string ResourcePrefix = "HtmlExport.";

  private readonly string _Text;

  private HtmlTemplate(string text)
  {
    _Text = text;
  }

  public static HtmlTemplate Load(string name)
  {
    var text = ReadResource(name);
    return new(text.TrimEnd('\n'));
  }

  public static string ReadResource(string name)
  {
    var assembly = typeof(HtmlTemplate).Assembly;
    using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name)!;
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }

  // A slot missing on either side throws, so a template and its caller can't drift apart silently.
  public HtmlContent Fill(params (string Slot, HtmlContent Content)[] contents)
  {
    var markups = contents.ToDictionary(content => content.Slot, content => content.Content.Markup);
    var unused = markups.Keys.ToHashSet();
    string FillSlot(Match match)
    {
      var slot = match.Groups[1].Value;
      unused.Remove(slot);
      return markups[slot];
    }

    var filled = SlotPattern().Replace(_Text, FillSlot);
    if (unused.Count > 0)
    {
      var names = string.Join(", ", unused);
      throw new InvalidOperationException($"The template has no slot named {names}.");
    }

    return HtmlContent.Raw(filled);
  }

  [GeneratedRegex(@"\{\{(\w+)\}\}")]
  private static partial Regex SlotPattern();
}
