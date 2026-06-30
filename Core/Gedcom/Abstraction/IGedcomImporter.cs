using GT4.Core.Project.Abstraction;

namespace GT4.Core.Gedcom.Abstraction;

public interface IGedcomImporter
{
  /// <summary>
  /// Reads a GEDCOM 5.5.1 stream and writes the individuals, names and family relationships it
  /// describes into <paramref name="document"/>. The whole import runs as a single transaction, so it
  /// either lands completely or not at all.
  /// </summary>
  /// <param name="mediaBasePath">
  /// Directory the GEDCOM file lives in, used to resolve external <c>OBJE</c> <c>FILE</c> image references
  /// into the person's photo set. When null (or a referenced file cannot be found) such references are not
  /// loaded and survive verbatim as residue instead.
  /// </param>
  Task ImportAsync(IProjectDocument document, TextReader reader, CancellationToken token, string? mediaBasePath = null);
}
