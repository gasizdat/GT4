using GT4.Core.Project.Abstraction;

namespace GT4.Core.Project;

/// <summary>
/// Base for managers and providers that operate over the whole project document. It holds the
/// owning <see cref="Document"/>. Tables depend only on the connection slice and derive from
/// <see cref="TableBase"/> instead.
/// </summary>
public abstract class ProjectComponentBase
{
  protected ProjectComponentBase(IProjectDocument document) => Document = document;

  internal IProjectDocument Document { get; init; }
}
