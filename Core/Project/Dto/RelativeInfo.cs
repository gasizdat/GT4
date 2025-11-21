using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class RelativeInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto,
  RelationshipType Type,
  Date? Date,
  bool ForwardLink
) : Relative(Id, BirthDate, DeathDate, BiologicalSex, Type, Date, ForwardLink)
{
  public RelativeInfo(Relative relative, Name[] names, Data? mainPhoto)
    : this(
        relative.Id, 
        relative.BirthDate, 
        relative.DeathDate, 
        relative.BiologicalSex,
        names, 
        mainPhoto, 
        relative.Type, 
        relative.Date, 
        relative.ForwardLink
  )
  {
  }

  public RelativeInfo(Person person, Name[] names, Data? mainPhoto,  RelationshipType type,  Date? date, bool forwardLink)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        names,
        mainPhoto,
        type,
        date,
        forwardLink
  )
  {
  }

  public RelativeInfo(PersonInfo person, RelationshipType type, Date? date, bool forwardLink)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        person.Names,
        person.MainPhoto,
        type,
        date,
        forwardLink
  )
  {
  }

  public static implicit operator PersonInfo(RelativeInfo relativeInfo) =>
    new PersonInfo(person: relativeInfo, names: relativeInfo.Names, mainPhoto: relativeInfo.MainPhoto);
}
