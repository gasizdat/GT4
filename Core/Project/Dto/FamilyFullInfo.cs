namespace GT4.Core.Project.Dto;

public record class FamilyFullInfo(
  int Id,
  string Value,
  NameType Type,
  int? ParentId,
  Data? MainPhoto,
  Data[] AdditionalPhotos,
  Data[] Attachments
) : Name(Id, Value, Type, ParentId)
{
  public FamilyFullInfo(Name name, Data? mainPhoto, Data[] additionalPhotos, Data[] attachments)
    : this(name.Id, name.Value, name.Type, name.ParentId, mainPhoto, additionalPhotos, attachments)
  {
  }
}
