using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Tests;

internal static class Utils
{
  public static IEnumerable<int> Id(this IEnumerable<ElementId> elements) =>
    elements.Select(e => e.Id);

  public static T SingleId<T>(this IEnumerable<T> elementIds, ElementId elementId) where T : ElementId =>
    elementIds.Single(r => r.Id == elementId.Id);
}
