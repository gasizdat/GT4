namespace GT4.Core.Project.Dto;

public record class PersonFullInfo(
  PersonInfo PersonInfo,
  Data[] AdditionalPhotos,
  RelativeInfo[] RelativeInfos,
  Data? Biography
  ) : PersonInfo(original: PersonInfo)
{
  public PersonFullInfo(
    Person person,
    Name[] names,
    Data? mainPhoto,
    Data[] additionalPhotos,
    RelativeInfo[] relativeInfos,
    Data? biography)
    : this(
        PersonInfo: new PersonInfo(Person: person, Names: names, MainPhoto: mainPhoto),
        AdditionalPhotos: additionalPhotos,
        RelativeInfos: relativeInfos,
        Biography: biography)
  {
  }
}
