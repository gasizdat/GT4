using System.Text;

namespace GT4.Core.Gedcom;

/// <summary>
/// Serializes a <see cref="GedcomNode"/> record tree back to GEDCOM text. Values that exceed the line
/// length limit are split across CONC lines, and embedded newlines become CONT lines, so any value the
/// <see cref="GedcomReader"/> folded together is reproduced faithfully.
/// </summary>
internal static class GedcomWriter
{
  // GEDCOM 5.5.1 caps a physical line at 255 chars; leave headroom for the "level CONC " prefix.
  private const int MaxValueLength = 200;

  public static void Write(TextWriter writer, GedcomNode record) => WriteNode(writer, record, 0);

  private static void WriteNode(TextWriter writer, GedcomNode node, int level)
  {
    WriteValueLines(writer, level, node.Xref, node.Tag, node.Value);
    foreach (var child in node.Children)
    {
      WriteNode(writer, child, level + 1);
    }
  }

  private static void WriteValueLines(TextWriter writer, int level, string? xref, string tag, string? value)
  {
    var head = new StringBuilder().Append(level);
    if (xref is not null)
    {
      head.Append(' ').Append(xref);
    }
    head.Append(' ').Append(tag);

    if (string.IsNullOrEmpty(value))
    {
      writer.WriteLine(head.ToString());
      return;
    }

    var headLineWritten = false;
    var paragraphs = value.Split('\n');
    foreach (var paragraph in paragraphs)
    {
      var chunks = Chunk(paragraph, MaxValueLength);
      for (var i = 0; i < chunks.Count; i++)
      {
        if (!headLineWritten)
        {
          writer.WriteLine($"{head} {chunks[i]}");
          headLineWritten = true;
        }
        else
        {
          var continuationTag = i == 0 ? GedcomTags.Continuation : GedcomTags.Concatenation;
          WriteContinuation(writer, level + 1, continuationTag, chunks[i]);
        }
      }
    }
  }

  private static void WriteContinuation(TextWriter writer, int level, string tag, string chunk)
  {
    var line = chunk.Length > 0 ? $"{level} {tag} {chunk}" : $"{level} {tag}";
    writer.WriteLine(line);
  }

  private static List<string> Chunk(string text, int max)
  {
    if (text.Length <= max)
      return [text];

    var chunks = new List<string>();
    for (var offset = 0; offset < text.Length; offset += max)
    {
      var length = Math.Min(max, text.Length - offset);
      chunks.Add(text.Substring(offset, length));
    }
    return chunks;
  }
}
