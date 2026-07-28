using GT4.Core.Project.Abstraction;

namespace GT4.Core.Gedcom.Abstraction;

public interface IGedcomExporter
{
  /// <summary>
  /// Writes the whole <paramref name="document"/> as a GEDCOM 5.5.1 document. The declared character set
  /// is UTF-8, so <paramref name="writer"/> must encode as UTF-8 for the output to be valid.
  /// </summary>
  /// <param name="media">
  /// Receives every photo and attachment as its own file; the document references them by relative
  /// <c>OBJE FILE</c> path, so an export is only complete alongside what this sink collected.
  /// </param>
  Task ExportAsync(IProjectDocument document, TextWriter writer, IGedcomMediaWriter media, CancellationToken token);
}
