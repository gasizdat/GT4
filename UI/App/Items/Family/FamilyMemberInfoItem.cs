using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.App.Items;

public class FamilyMemberInfoItem : PersonInfoItem
{
  private readonly IDateFormatter _DateFormatter;

  public FamilyMemberInfoItem(Person person, ServiceProvider services)
    : base(person, services.GetRequiredService<INameFormatter>())
  {
    _DateFormatter = services.GetRequiredService<IDateFormatter>();
  }

  public string DayOfBirth => 
    string.Format(UIStrings.FieldDateOfBirth_1, _DateFormatter.ToString(Person.BirthDate, Person.BirthDateStatus));
  public string DayOfDeath => Person.DeathDateStatus.HasValue ?
    string.Format(UIStrings.FieldDateOfDeath_1, _DateFormatter.ToString(Person.DeathDate, Person.DeathDateStatus.Value)) : string.Empty;
}
