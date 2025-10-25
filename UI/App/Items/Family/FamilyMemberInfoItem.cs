using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.App.Items;

public class FamilyMemberInfoItem : PersonInfoItem
{
  private readonly IDateFormatter _DateFormatter;
  private readonly IDateSpanFormatter _DateSpanFormatter;

  public FamilyMemberInfoItem(Person person, ServiceProvider services)
    : base(person, services.GetRequiredService<INameFormatter>())
  {
    _DateFormatter = services.GetRequiredService<IDateFormatter>();
    _DateSpanFormatter = services.GetRequiredService<IDateSpanFormatter>();
  }

  public string DateOfBirth => 
    string.Format(UIStrings.FieldDateOfBirth_1, _DateFormatter.ToString(Person.BirthDate));
  public string DateOfDeath => 
    string.Format(UIStrings.FieldDateOfDeath_1, _DateFormatter.ToString(Person.DeathDate));
  public bool ShowDateOfDeath => Person.DeathDate.HasValue;
  public string Age
  {
    get
    {
      var ofDate = Person.DeathDate.HasValue ? Person.DeathDate.Value : Date.Now;
      var age = ofDate - Person.BirthDate;
      var ageText = _DateSpanFormatter.ToString(age);

      return string.Format(UIStrings.FieldAge_1, ageText);
    }
  }
  
}
