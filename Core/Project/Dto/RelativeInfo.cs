namespace GT4.Core.Project.Dto;

public record class RelativeInfo(
  Relative Relative,
  Name[] Names,
  Data? MainPhoto)
  : Relative(original: Relative)
{
  public static implicit operator PersonInfo(RelativeInfo relativeInfo) => 
    new PersonInfo(person: relativeInfo, names: relativeInfo.Names, mainPhoto: relativeInfo.MainPhoto);
}
