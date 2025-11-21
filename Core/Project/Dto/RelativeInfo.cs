using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class RelativeInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  RelationshipType Type,
  Date? Date,
  Name[] Names,
  Data? MainPhoto
) : Relative(Id, BirthDate, DeathDate, BiologicalSex, Type, Date)
{
  public RelativeInfo(Relative relative, Name[] names, Data? mainPhoto)
    : this(
        relative.Id, 
        relative.BirthDate, 
        relative.DeathDate, 
        relative.BiologicalSex, 
        relative.Type, 
        relative.Date, 
        names, 
        mainPhoto
  )
  {
  }

  public RelativeInfo(Person person,  RelationshipType type,  Date? date, Name[] names, Data? mainPhoto)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        type,
        date,
        names,
        mainPhoto
  )
  {
  }

  public static implicit operator PersonInfo(RelativeInfo relativeInfo) =>
    new PersonInfo(person: relativeInfo, names: relativeInfo.Names, mainPhoto: relativeInfo.MainPhoto);
}
