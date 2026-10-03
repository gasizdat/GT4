using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

// A person's hand-placed column in the family tree centred on centerId, in slot offsets from the centre.
public interface IFamilyTreeArrangementStore
{
  IReadOnlyDictionary<int, double> Get(ProjectInfo project, int centerId);
  void Set(ProjectInfo project, int centerId, IReadOnlyDictionary<int, double> offsets);
  void Clear(ProjectInfo project, int centerId);
}
