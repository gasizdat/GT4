using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class ProjectInfo(
  string Name,
  string Description,
  long? Revision,
  FileDescription Origin,
  MainPersonInfo? MainPerson = null
)
{
  public const long InitialRevision = 0;
}

public record class MainPersonInfo(int PersonId, string DisplayName);
