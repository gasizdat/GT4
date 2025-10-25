using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class FamilyInfoItem
{
  private readonly Name _FamilyName;
  private readonly PersonInfoItem[] _Persons;
  private ImageSource _DefaultFamilyImage => ImageSource.FromStream(token => FileSystem.OpenAppPackageFileAsync("family_stub.png"));

  public FamilyInfoItem(Name familyName, PersonInfoItem[] persons)
  {
    _FamilyName = familyName;
    _Persons = persons;
  }

  public Name FamilyName => _FamilyName;
  public PersonInfoItem[] Persons => _Persons;
  public virtual ImageSource FamilyImage => _DefaultFamilyImage;
  public virtual bool IsHandlesVisible => true;
}
