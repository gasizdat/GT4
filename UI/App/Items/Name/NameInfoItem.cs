using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class NameInfoItem : CollectionItemBase<Name>
{
  private readonly INameTypeFormatter _NameTypeFormatter;

  public NameInfoItem(Name name, INameTypeFormatter nameTypeFormatter)
    : base(name, string.Empty)
  {
    _NameTypeFormatter = nameTypeFormatter;
  }

  public string Value => Info.Value;
  public string Type => _NameTypeFormatter.ToString(Info.Type);
}
