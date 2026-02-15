using GT4.Core.Project.Dto;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Utils.Comparers;

public class PersonInfoComparer : IComparer<PersonInfo>
{
  private readonly INameFormatter _NameFormatter;

  public PersonInfoComparer(INameFormatter nameFormatter)
  {
    _NameFormatter = nameFormatter;
  }

  public int Compare(PersonInfo? x, PersonInfo? y)
  {
    var xName = x is null ? null : _NameFormatter.ToString(x, NameFormat.CommonPersonName);
    var yName = y is null ? null : _NameFormatter.ToString(y, NameFormat.CommonPersonName);

    return xName?.CompareTo(yName) ?? 0;
  }
}
