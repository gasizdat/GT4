using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.Items;

public class FamilyMemberInfoItem : PersonInfoItem
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IDateSpanFormatter _DateSpanFormatter;

  protected FamilyMemberInfoItem(string itemName, ServiceProvider services)
    : this(personInfo: new PersonInfo(
        Id: TableBase.NonCommitedId,
        BirthDate: default,
        DeathDate: default,
        BiologicalSex: default,
        Names:[ new Name(
          Id: TableBase.NonCommitedId,
          Value: itemName,
          Type: NameType.AdditionalName,
          ParentId: default) ],
        MainPhoto: default),
      services: services)
  {
  }

  public FamilyMemberInfoItem(PersonInfo personInfo, ServiceProvider services)
    : base(personInfo, services.GetRequiredService<INameFormatter>())
  {
    _DateFormatter = services.GetRequiredService<IDateFormatter>();
    _DateSpanFormatter = services.GetRequiredService<IDateSpanFormatter>();
  }

  public bool ShowDateOfDeath => Info.DeathDate.HasValue;
  public string Age
  {
    get
    {
      var ofDate = Info.DeathDate.HasValue ? Info.DeathDate.Value : Date.Now;
      var age = ofDate - Info.BirthDate;
      var ageText = _DateSpanFormatter.ToString(age);

      return string.Format(UIStrings.FieldAge_1, ageText);
    }
  }
}
