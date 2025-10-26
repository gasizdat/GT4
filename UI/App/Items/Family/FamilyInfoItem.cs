using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class FamilyInfoItem : CollectionItemBase<Name>
{
  private readonly PersonInfoItem[] _Persons;

  public FamilyInfoItem(Name familyName, PersonInfoItem[] persons)
    : base(familyName, "family_stub.png")
  {
    _Persons = persons;
  }

  public PersonInfoItem[] Persons => _Persons;
}
