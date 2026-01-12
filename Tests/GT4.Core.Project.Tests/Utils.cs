using GT4.Core.Project.Dto;

namespace GT4.Core.Project.Tests;

internal static class Utils
{
  public static IEnumerable<int> Id(this IEnumerable<ElementId> elements)
  {
    return elements.Select(e => e.Id);
  }
}
