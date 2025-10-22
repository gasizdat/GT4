using GT4.Core.Project.Dto;

namespace GT4.UI;

public class FamilyInfoItem
{
  private readonly Name _familyName;
  private readonly PersonInfoItem[] _persons;
  private ImageSource _DefaultFamilyImage => ImageSource.FromStream(token => FileSystem.OpenAppPackageFileAsync("family_stub.png"));

  public FamilyInfoItem(Name familyName, PersonInfoItem[] persons)
  {
    _familyName = familyName;
    _persons = persons;
  }

  public Name FamilyName => _familyName;
  public PersonInfoItem[] Persons => _persons;
  public virtual ImageSource FamilyImage => _DefaultFamilyImage;
  public virtual bool IsHandlesVisible => true;
}
