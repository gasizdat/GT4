namespace GT4.Core.Project.Dto;

public record class Parents(
  RelativeFullInfo[] Native,
  RelativeFullInfo[] Adoptive,
  RelativeFullInfo[] Step
);