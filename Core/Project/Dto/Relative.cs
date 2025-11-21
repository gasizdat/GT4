using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class Relative(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  RelationshipType Type,
  Date? Date
) : Person(Id, BirthDate, DeathDate, BiologicalSex)
{
  public Relative(Person person, RelationshipType type, Date? date)
    : this(
        person.Id, 
        person.BirthDate, 
        person.DeathDate, 
        person.BiologicalSex, 
        type, 
        date
  )
  {
  }
}