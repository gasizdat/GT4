using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IFamilyTreeProvider
{
  /// <summary>
  /// Builds an ancestor-descendant graph centred on <paramref name="center"/>, walking up to
  /// <paramref name="ancestorGenerations"/> levels of parents, down to
  /// <paramref name="descendantGenerations"/> levels of children, and attaching the spouse of every
  /// person reached along the way.
  /// </summary>
  Task<FamilyTree> BuildAsync(
    Person center,
    int ancestorGenerations,
    int descendantGenerations,
    CancellationToken token);
}
