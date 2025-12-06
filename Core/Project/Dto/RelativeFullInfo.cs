using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class RelativeFullInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto,
  Data[] AdditionalPhotos,
  RelativeInfo[] RelativeInfos,
  Data? Biography,
  RelationshipType Type,
  Date? Date
) : RelativeInfo(Id, BirthDate, DeathDate, BiologicalSex, Names, MainPhoto, Type, Date)
{
  public RelativeFullInfo(
    PersonFullInfo person,
    RelationshipType type,
    Date? date)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate, 
        person.BiologicalSex, 
        person.Names, 
        person.MainPhoto, 
        person.AdditionalPhotos, 
        person.RelativeInfos, 
        person.Biography,
        type,
        date)
  {
  }
}
