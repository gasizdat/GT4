using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class Relative(
  Person Person, 
  RelativeType Type, 
  Date? Date
);
