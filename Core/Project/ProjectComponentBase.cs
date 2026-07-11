using GT4.Core.Project.Abstraction;

namespace GT4.Core.Project;

/// <summary>
/// Base for components that operate over a project document (tables, managers, providers). It holds the
/// owning <see cref="Document"/>. Table-only concerns (schema creation, raw reader helpers) live on
/// <see cref="TableBase"/>.
/// </summary>
public abstract class ProjectComponentBase
{
  protected ProjectComponentBase(IProjectDocument document) => Document = document;

  internal IProjectDocument Document { get; init; }
}
