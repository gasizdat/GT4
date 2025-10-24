using GT4.Core.Project.Dto;

namespace GT4.UI;

public class FamilyMemberInfoItem : PersonInfoItem
{
  private readonly IDateFormatter _DateFormatter;

  public FamilyMemberInfoItem(Person person, ServiceProvider services)
    : base(person, services.GetRequiredService<INameFormatter>())
  {
    _DateFormatter = services.GetRequiredService<IDateFormatter>();
  }

  public string DayOfBirth => _DateFormatter.ToString(Person.BirthDate, Person.BirthDateStatus);
  public string DayOfDeath => Person.DeathDateStatus.HasValue ?
    _DateFormatter.ToString(Person.DeathDate, Person.DeathDateStatus.Value) : string.Empty;
}
