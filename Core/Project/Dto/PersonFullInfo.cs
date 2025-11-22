using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class PersonFullInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto,
  Data[] AdditionalPhotos,
  RelativeInfo[] RelativeInfos,
  Data? Biography
) : PersonInfo(Id, BirthDate, DeathDate, BiologicalSex, Names, MainPhoto)
{
  public PersonFullInfo(
    PersonInfo person,
    Data[] additionalPhotos,
    RelativeInfo[] relativeInfos,
    Data? biography)
    : this(
        person.Id, 
        person.BirthDate, 
        person.DeathDate, 
        person.BiologicalSex, 
        person.Names, 
        person.MainPhoto,
        additionalPhotos,
        relativeInfos,
        biography
  )
  {
  }

  public PersonFullInfo(
    Person person,
    Name[] names,
    Data? mainPhoto,
    Data[] additionalPhotos,
    RelativeInfo[] relativeInfos,
    Data? biography)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        names,
        mainPhoto,
        additionalPhotos,
        relativeInfos,
        biography
  )
  {
  }

  public static readonly PersonFullInfo Empty = new PersonFullInfo(
    Id: TableBase.NonCommitedId,
    BirthDate: Date.Now,
    DeathDate: null,
    BiologicalSex: default,
    Names: [],
    MainPhoto: null,
    AdditionalPhotos: [],
    RelativeInfos: [],
    Biography: null
  );
}
