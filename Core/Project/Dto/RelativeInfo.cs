using GT4.Core.Utils;
using System.Diagnostics.CodeAnalysis;

namespace GT4.Core.Project.Dto;

public record class RelativeInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto,
  RelationshipType Type,
  Date? Date
) : Relative(Id, BirthDate, DeathDate, BiologicalSex, Type, Date)
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
        relative.Date
  )
  {
  }

  public RelativeInfo(Person person, Name[] names, Data? mainPhoto,  RelationshipType type,  Date? date)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        names,
        mainPhoto,
        type,
        date
  )
  {
  }

  public RelativeInfo(PersonInfo person, RelationshipType type, Date? date)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        person.Names,
        person.MainPhoto,
        type,
        date
  )
  {
  }

  [return: NotNullIfNotNull(nameof(relativeInfo))]
  public static implicit operator PersonInfo?(RelativeInfo? relativeInfo) =>
    relativeInfo is null 
    ? null
    : new PersonInfo(person: relativeInfo, names: relativeInfo.Names, mainPhoto: relativeInfo.MainPhoto);
}
