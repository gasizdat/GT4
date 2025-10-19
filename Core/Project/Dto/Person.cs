namespace GT4.Core.Project.Dto;

  public record class Person(
    int Id,
    Name[] Names,
    DateTime? BirthDate,
    DateStatus BirthDateStatus,
    DateTime? DeathDate,
    DateStatus? DeathDateStatus,
    BiologicalSex BiologicalSex
  );
