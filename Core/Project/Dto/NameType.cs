namespace GT4.Core.Project.Dto;

[Flags]
public enum NameType
{
  // For the internal use only
  AllNames = 0,
  // The name of a family
  FamilyName = 0x1,
  // The first name of a person
  FirstName = 0x2,
  // The optional middle name of the person
  MiddleName = 0x4,
  // The optional last name of the person
  LastName = 0x8,
  // The optional additional name (e.g., nickname, alias)
  AdditionalName = 0xf,
  // The declension of a name in the Male gender (For languages ​​with gender declension)
  MaleDeclension = 0x10,
  // The declension of a name in the Female gender (For languages ​​with gender declension)
  FemaleDeclension = 0x20,
  // The mask for the name part without decension
  NoDeclension = ~(MaleDeclension | FemaleDeclension)
}
