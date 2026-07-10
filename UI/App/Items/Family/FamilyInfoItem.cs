using GT4.Core.Project.Dto;

namespace GT4.UI.Items;

public class FamilyInfoItem : CollectionItemBase<Name>
{
  private readonly PersonInfo[] _Persons;

  public FamilyInfoItem(Name familyName, PersonInfo[] persons)
    : base(familyName, "family_stub.png")
  {
    _Persons = persons;
  }

  public PersonInfo[] Persons => _Persons;
}
