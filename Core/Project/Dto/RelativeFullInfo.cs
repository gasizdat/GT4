using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class RelativeFullInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto,
  RelationshipType Type,
  Date? Date,
  Generation Generation,
  Consanguinity Consanguinity,
  RelativeInfo[] RelativeInfos
) : RelativeInfo(Id, BirthDate, DeathDate, BiologicalSex, Names, MainPhoto, Type, Date, Generation, Consanguinity)
{
  public RelativeFullInfo(
    RelativeInfo relative,
    RelativeInfo[] relativeInfos)
    : this(
        relative.Id,
        relative.BirthDate,
        relative.DeathDate, 
        relative.BiologicalSex, 
        relative.Names,
        relative.MainPhoto,
        relative.Type,
        relative.Date,
        relative.Generation,
        relative.Consanguinity,
        relativeInfos)
  {
  }
}
