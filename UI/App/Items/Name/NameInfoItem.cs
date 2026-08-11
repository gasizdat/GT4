using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Items;

public class NameInfoItem : CollectionItemBase<Name>
{
  private readonly INameTypeFormatter _NameTypeFormatter;

  public NameInfoItem(Name name, INameTypeFormatter nameTypeFormatter, ImageUtils imageUtils)
    : base(name, string.Empty, imageUtils)
  {
    _NameTypeFormatter = nameTypeFormatter;
  }

  public string Value => Info.Value;
  public string Type => _NameTypeFormatter.ToString(Info.Type);
  public bool CanBeRemoved => Info.Type != NameType.FamilyName;
}
