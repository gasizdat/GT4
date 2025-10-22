namespace GT4.Core.Project.Dto;

public record class Person(
  int Id,
  Name[] Names,
  byte[]? MainPhoto,
  DateTime? BirthDate,
  DateStatus BirthDateStatus,
  DateTime? DeathDate,
  DateStatus? DeathDateStatus,
  BiologicalSex BiologicalSex
);
