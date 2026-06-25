using GT4.Core.Project.Abstraction;

namespace GT4.Core.Project;

/// <summary>
/// Base for components that operate over a project document (tables, managers, providers). It holds the
/// owning <see cref="Document"/> and the shared non-committed id sentinel. Table-only concerns (schema
/// creation, raw reader helpers) live on <see cref="TableBase"/>.
/// </summary>
public abstract class ProjectComponentBase
{
  protected ProjectComponentBase(IProjectDocument document) => Document = document;

  public static readonly int NonCommittedId = 0;

  internal IProjectDocument Document { get; init; }
}
