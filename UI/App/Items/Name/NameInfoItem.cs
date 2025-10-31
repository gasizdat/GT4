using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.App.Items;

public class NameInfoItem : CollectionItemBase<Name>
{
  public NameInfoItem(Name name)
    : base(name, string.Empty)
  {
  }

  public string Value => Info.Value;
  public string Type
  {
    get
    {
      switch (Info.Type)
      {
        case NameType.FirstName:
          return UIStrings.FieldFirstName;
        case NameType.LastName:
          return UIStrings.FieldLastName;
        case NameType.MiddleName:
          return UIStrings.FieldMiddleName;
        case NameType.FamilyName:
          return UIStrings.FieldFamilyName;
        case NameType.AdditionalName:
          return UIStrings.FieldAdditionalName;

        default:
          return string.Empty;
      }
    }
  }
}
