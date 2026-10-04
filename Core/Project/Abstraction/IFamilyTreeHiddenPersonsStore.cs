using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IFamilyTreeHiddenPersonsStore
{
  int[] Get(ProjectInfo project);
  void Set(ProjectInfo project, IEnumerable<int> personIds);
}
