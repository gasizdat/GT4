using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IMainPersonStore
{
  MainPersonInfo? Get(ProjectInfo project);
  void Set(ProjectInfo project, int personId, string displayName);
  void Clear(ProjectInfo project);
}

public record class MainPersonInfo(int PersonId, string DisplayName);
