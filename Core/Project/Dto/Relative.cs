namespace GT4.Core.Project.Dto;

public record class Relative(
  Person Person, 
  RelativeType Type, 
  DateOnly? Date,
  DateStatus? DateStatus
);
