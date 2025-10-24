namespace GT4.Core.Project.Dto;

public record class PersonData(
  int Id,
  Person Person,
  Data Data,
  DataCategory Category
);
