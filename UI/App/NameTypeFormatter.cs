using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI;

public class NameTypeFormatter : INameTypeFormatter
{
  public string ToString(NameType type)
  {
    switch (type & ~(NameType.MaleDeclension | NameType.FemaleDeclension))
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