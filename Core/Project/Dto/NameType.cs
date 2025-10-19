namespace GT4.Core.Project.Dto;

public enum NameType
{
  // For the internal use only
  AllNames = 0,
  // The name of a family
  FamilyName = 1,
  // The first name of a person
  FirstName = 2,
  // The optional middle name of the person
  MiddleName = 3,
  // The optional last name of the person
  LastName = 4,
  // The optional additional name (e.g., nickname, alias)
  AdditionalName = 5,
  // The declension of a name in the Male gender (For languages ​​with gender declension)
  MaleName = 6,
  // The declension of a name in the Female gender (For languages ​​with gender declension)
  FemaleName = 7,
}
