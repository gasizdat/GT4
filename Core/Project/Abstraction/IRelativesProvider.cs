using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IRelativesProvider
{
  Task<Parents> GetParentsAsync(RelativeInfo[] relativeInfos, MainPhoto mainPhoto, CancellationToken token);
  Task<RelativeInfo[]> GetStepChildrenAsync(RelativeInfo[] relativeInfos, MainPhoto mainPhoto, CancellationToken token);
  RelativeInfo[] GetAdoptiveChildren(RelativeInfo[] relativeInfos);
  RelativeInfo[] GetChildren(RelativeInfo[] relativeInfos);
  Siblings GetSiblings(Person person, Parents parents);
  Task<RelativeInfo[]> GetRelativeInfosAsync(Person person, MainPhoto mainPhoto, CancellationToken token);
  Task<RelativeInfo[]> GetRelativeInfosAsync(RelativeInfo relativeInfo, MainPhoto mainPhoto, CancellationToken token);
}
