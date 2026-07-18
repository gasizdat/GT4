using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IKinshipFinder
{
  Task<RelativeInfo[]?> FindPathAsync(Person source, Person target, CancellationToken token);
}
