using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IMainPersonStore
{
  int? Get(ProjectInfo project);
  void Set(ProjectInfo project, int personId);
  void Clear(ProjectInfo project);
}
