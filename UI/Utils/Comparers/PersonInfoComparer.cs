using GT4.Core.Project.Dto;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Utils.Comparers;

public class PersonInfoComparer : IComparer<PersonInfo>
{
  private readonly INameFormatter _NameFormatter;
  private readonly NameFormat _NameFormat;

  public PersonInfoComparer(INameFormatter nameFormatter, NameFormat nameFormat = NameFormat.CommonPersonName)
  {
    _NameFormatter = nameFormatter;
    _NameFormat = nameFormat;
  }

  public int Compare(PersonInfo? x, PersonInfo? y)
  {
    var xName = x is null ? null : _NameFormatter.ToString(x, _NameFormat);
    var yName = y is null ? null : _NameFormatter.ToString(y, _NameFormat);

    return xName?.CompareTo(yName) ?? 0;
  }
}
