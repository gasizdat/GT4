namespace GT4.Core.Project.Abstraction;

/// <summary>
/// Thrown when a project file was written by a newer build than the one running. Nothing this build
/// can do makes such a file readable — the app itself has to be updated.
/// </summary>
public sealed class ProjectSchemaTooNewException : InvalidOperationException
{
  public ProjectSchemaTooNewException(int fileVersion, int supportedVersion)
    : base($"The project schema version {fileVersion} is newer than the supported version {supportedVersion}.")
  {
  }
}
