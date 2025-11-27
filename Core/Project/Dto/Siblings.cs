namespace GT4.Core.Project.Dto;

public record class Siblings(
  RelativeInfo[] Native,
  RelativeInfo[] ByFather,
  RelativeInfo[] ByMother,
  RelativeInfo[] Adoptive,
  RelativeInfo[] Step
);
