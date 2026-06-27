using GT4.Core.Project.Abstraction;

namespace GT4.Core.Gedcom.Abstraction;

public interface IGedcomImporter
{
  /// <summary>
  /// Reads a GEDCOM 5.5.1 stream and writes the individuals, names and family relationships it
  /// describes into <paramref name="document"/>. The whole import runs as a single transaction, so it
  /// either lands completely or not at all.
  /// </summary>
  Task ImportAsync(IProjectDocument document, TextReader reader, CancellationToken token);
}
