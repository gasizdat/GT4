using GT4.Core.Project.Abstraction;

namespace GT4.Core.Gedcom.Abstraction;

public interface IGedcomExporter
{
  /// <summary>
  /// Writes the whole <paramref name="document"/> as a GEDCOM 5.5.1 document. The declared character set
  /// is UTF-8, so <paramref name="writer"/> must encode as UTF-8 for the output to be valid.
  /// </summary>
  Task ExportAsync(IProjectDocument document, TextWriter writer, CancellationToken token);
}
