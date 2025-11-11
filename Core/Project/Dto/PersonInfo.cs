using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class PersonInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto)
  : Person(
    Id: Id,
    BirthDate: BirthDate,
    DeathDate: DeathDate,
    BiologicalSex: BiologicalSex)
{
  public PersonInfo(Person person, Name[] names, Data? mainPhoto)
    : this (Id: person.Id, 
            BirthDate: person.BirthDate, 
            DeathDate: person.DeathDate, 
            BiologicalSex: person.BiologicalSex, 
            Names: names, 
            MainPhoto: mainPhoto)
  {
  }
}