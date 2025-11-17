namespace GT4.Core.Project.Dto;

public record class PersonInfo(
  Person Person,
  Name[] Names,
  Data? MainPhoto)
  : Person(original: Person)
{
}