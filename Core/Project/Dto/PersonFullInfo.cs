using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class PersonFullInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto,
  Data[]? AdditionalPhotos,
  Relative[]? Relatives,
  Data? Biography
  ) : PersonInfo(
    Id: Id,
    BirthDate: BirthDate,
    DeathDate: DeathDate,
    BiologicalSex: BiologicalSex,
    Names: Names,
    MainPhoto: MainPhoto)
{
  public PersonFullInfo(PersonInfo personInfo, Data[]? additionalPhotos, Relative[]? relatives, Data? biography)
  : this(
      Id: personInfo.Id,
      BirthDate: personInfo.BirthDate,
      DeathDate: personInfo.DeathDate,
      BiologicalSex: personInfo.BiologicalSex,
      Names: personInfo.Names,
      MainPhoto: personInfo.MainPhoto,
      AdditionalPhotos: additionalPhotos,
      Relatives: relatives,
      Biography: biography)
  {
  }
}
