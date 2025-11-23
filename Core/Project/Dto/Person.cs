using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class Person(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex
) : ElementId(Id);
