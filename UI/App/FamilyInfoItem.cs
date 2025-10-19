using GT4.Core.Project.Dto;

namespace GT4.UI;

public class FamilyInfoItem
{
  private readonly Name _familyName;

  public FamilyInfoItem(Name familyName)
  {
    _familyName = familyName;
  }

  public Name FamilyName => _familyName;
}
