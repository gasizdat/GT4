using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class PersonInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto
) : Person(Id, BirthDate, DeathDate, BiologicalSex)
{
  public PersonInfo(Person person, Name[] names, Data? mainPhoto)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        names,
        mainPhoto
  )
  {
  }
}