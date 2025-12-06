using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IRelativesProvider
{
  Task<Parents> GetParentsAsync(RelativeInfo[] relativeInfos, CancellationToken token);
  Task<RelativeInfo[]> GetStepChildrenAsync(RelativeInfo[] relativeInfos, CancellationToken token);
  RelativeInfo[] AdoptiveChildren(RelativeInfo[] relativeInfos);
  RelativeInfo[] Children(RelativeInfo[] relativeInfos);
  Siblings GetSiblings(Person person, Parents parents);
  Task<RelativeInfo[]> GetRelativeInfosAsync(Person person, bool selectMainPhoto, CancellationToken token);
}
