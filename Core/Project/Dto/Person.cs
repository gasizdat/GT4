namespace GT4.Core.Project.Dto;

  public record class Person(
    int Id,
    Name Name,
    Name? Name2,
    Name? Name3,
    Name? Name4,
    Name? Name5,
    Name? Name6,
    Name? Name7,
    Name? Name8,
    Name? Name9,
    Name? Name10,
    DateTime? BirthDate,
    DateStatus BirthDateStatus,
    DateTime? DeathDate,
    DateStatus? DeathDateStatus,
    BiologicalSex BiologicalSex
  );
