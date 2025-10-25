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
    string.Format(UIStrings.FieldDateOfBirth_1, _DateFormatter.ToString(Person.BirthDate, Person.BirthDateStatus));
  public string DateOfDeath => 
    string.Format(UIStrings.FieldDateOfDeath_1, _DateFormatter.ToString(Person.DeathDate, 
      Person.DeathDateStatus.HasValue ? Person.DeathDateStatus!.Value : default));
  public bool ShowDateOfDeath => Person.DeathDateStatus.HasValue;
  public string Age
  {
    get
    {
      if (!Person.BirthDate.HasValue)
      {
        return string.Format(UIStrings.FieldAge_1, _DateFormatter.ToString(Person.BirthDate, Person.BirthDateStatus));
      }

      var lastDate = Person.DeathDateStatus.HasValue && Person.DeathDate.HasValue ? Person.DeathDate.Value : new DateOnly().Now();
      var age = Person.BirthDate.Value.Period(lastDate);
      var ageText = _DateSpanFormatter.ToString(age, Person.BirthDateStatus, 
        Person.DeathDateStatus.HasValue ? Person.DeathDateStatus!.Value : DateStatus.WellKnown);

      return string.Format(UIStrings.FieldAge_1, ageText);
    }
  }
  
}
