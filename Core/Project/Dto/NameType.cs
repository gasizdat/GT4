namespace GT4.Core.Project.Dto;

public enum NameType
{
  // For the internal use only
  AllNames = 0,
  // The first name of a person
  FirstName = 1,
  // The optional middle name of the person
  MiddleName = 2,
  // The optional last name of the person
  LastName = 3,
  // The optional additional name (e.g., nickname, alias)
  AdditionalName = 4,
  // The declension of a name in the Male gender (For languages ​​with gender declension)
  MaleName = 5,
  // The declension of a name in the Female gender (For languages ​​with gender declension)
  FemaleName = 6,
}
