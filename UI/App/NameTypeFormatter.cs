using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI;

public class NameTypeFormatter : INameTypeFormatter
{
  public string ToString(NameType type)
  {
    return (type & NameType.NoDeclension) switch
    {
      NameType.FirstName => UIStrings.FieldFirstName,
      NameType.LastName => UIStrings.FieldLastName,
      NameType.MiddleName => UIStrings.FieldMiddleName,
      NameType.FamilyName => UIStrings.FieldFamilyName,
      NameType.AdditionalName => UIStrings.FieldAdditionalName,
      _ => string.Empty
    };
  }
}