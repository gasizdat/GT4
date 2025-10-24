namespace GT4.Core.Project.Dto;

public record class Person(
  int Id,
  Name[] Names,
  byte[]? MainPhoto,
  DateOnly? BirthDate,
  DateStatus BirthDateStatus,
  DateOnly? DeathDate,
  DateStatus? DeathDateStatus,
  BiologicalSex BiologicalSex
);
