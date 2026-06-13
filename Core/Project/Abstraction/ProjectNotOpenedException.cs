namespace GT4.Core.Project.Abstraction;

/// <summary>
/// Thrown when project-scoped state is requested but no project is currently open. This is typically
/// a benign race during app teardown: the lifecycle closes the project while background work is still
/// in flight. Derives from <see cref="InvalidOperationException"/> so existing callers that catch
/// that base type keep working, while fire-and-forget UI paths can recognise and swallow it instead
/// of surfacing it as an error or crashing.
/// </summary>
public sealed class ProjectNotOpenedException : InvalidOperationException
{
  public ProjectNotOpenedException()
    : base("Project is not opened yet.")
  {
  }
}
