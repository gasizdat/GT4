using GT4.Core.Project.Dto;
using System.Diagnostics.CodeAnalysis;

namespace GT4.Core.Project;

internal class ElementIdComparer<TSource> : IEqualityComparer<TSource> where TSource : ElementId
{
  public bool Equals(TSource? x, TSource? y)
  {
    return x?.Id == y?.Id;
  }

  public int GetHashCode([DisallowNull] TSource obj)
  {
    return obj.Id;
  }
}
