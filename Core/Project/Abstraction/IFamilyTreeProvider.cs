using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Abstraction;

public interface IFamilyTreeProvider
{
  /// <summary>
  /// Builds an ancestor-descendant graph centred on <paramref name="center"/>, walking up to
  /// <paramref name="ancestorGenerations"/> levels of parents, down to
  /// <paramref name="descendantGenerations"/> levels of children, and attaching the spouse of every
  /// person reached along the way.
  /// <para>
  /// When <paramref name="includeCollaterals"/> is <see langword="true"/> the descendant walk is also
  /// seeded from every ancestor, so siblings, aunts/uncles and cousins branch off the ancestral line
  /// for a fuller chart. When <see langword="false"/> only the centre's own descendants are shown.
  /// </para>
  /// </summary>
  Task<FamilyTree> BuildAsync(
    Person center,
    int ancestorGenerations,
    int descendantGenerations,
    bool includeCollaterals,
    MainPhoto mainPhoto,
    CancellationToken token);
}
