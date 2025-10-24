namespace GT4.Core.Project.Dto;

public record class Relative(
  Person Person, 
  RelativeType Type, 
  DateTime? DateTime,
  DateStatus? DateStatus
);
