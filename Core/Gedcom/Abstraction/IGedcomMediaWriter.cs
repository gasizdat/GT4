namespace GT4.Core.Gedcom.Abstraction;

/// <summary>
/// Receives the media an export references by <c>OBJE FILE</c>. The seam is what keeps packaging out of
/// the exporter: a directory and a zip entry are both just sinks, so the exporter never learns which one
/// it is writing into.
/// </summary>
public interface IGedcomMediaWriter
{
  Task WriteAsync(string relativePath, byte[] content, CancellationToken token);
}
