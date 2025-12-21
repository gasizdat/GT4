using GT4.Core.Utils;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GT4.Core.Project.Dto;

[DebuggerDisplay("{BiologicalSex}, {Type}, {DisplayName}")]
public record class RelativeInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto,
  RelationshipType Type,
  Date? Date,
  Generation Generation,
  Consanguinity Consanguinity
) : Relative(Id, BirthDate, DeathDate, BiologicalSex, Type, Date)
{
  public RelativeInfo(Relative relative, Name[] names, Data? mainPhoto, Generation generation, Consanguinity consanguinity)
    : this(
        relative.Id, 
        relative.BirthDate, 
        relative.DeathDate, 
        relative.BiologicalSex,
        names, 
        mainPhoto, 
        relative.Type, 
        relative.Date,
        generation,
        consanguinity
  )
  {
  }

  public RelativeInfo(
    Person person, 
    Name[] names, 
    Data? mainPhoto,  
    RelationshipType type,  
    Date? date, 
    Generation generation, 
    Consanguinity consanguinity)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        names,
        mainPhoto,
        type,
        date, 
        generation,
        consanguinity
  )
  {
  }

  public RelativeInfo(PersonInfo person, RelationshipType type, Date? date, Generation generation, Consanguinity consanguinity)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        person.Names,
        person.MainPhoto,
        type,
        date,
        generation,
        consanguinity
  )
  {
  }

  [return: NotNullIfNotNull(nameof(relativeInfo))]
  public static implicit operator PersonInfo?(RelativeInfo? relativeInfo) =>
    relativeInfo is null 
    ? null
    : new PersonInfo(person: relativeInfo, names: relativeInfo.Names, mainPhoto: relativeInfo.MainPhoto);

  public string DisplayName => ((PersonInfo)this).DisplayName;
}
