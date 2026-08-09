namespace GT4.Core.Project.Dto;

public record class FamilyInfo(
  int Id,
  string Value,
  NameType Type,
  int? ParentId,
  Data? MainPhoto
) : Name(Id, Value, Type, ParentId)
{
  public FamilyInfo(Name name, Data? mainPhoto)
    : this(name.Id, name.Value, name.Type, name.ParentId, mainPhoto)
  {
  }
}
