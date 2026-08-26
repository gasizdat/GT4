namespace GT4.Core.Project.Abstraction;

/// <summary>
/// Thrown when a project file's schema predates the running code. The file is readable but its tables
/// do not match the queries this build issues, so it must go through
/// <see cref="IProjectList.UpgradeAsync"/> before it can be opened for use.
/// </summary>
public sealed class ProjectSchemaOutdatedException : InvalidOperationException
{
  public ProjectSchemaOutdatedException(int fileVersion, int supportedVersion)
    : base($"The project schema version {fileVersion} is older than the supported version {supportedVersion}.")
  {
  }
}
