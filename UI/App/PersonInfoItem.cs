using GT4.Core.Project.Dto;

namespace GT4.UI;

public class PersonInfoItem
{
  private readonly Person _person;

  public PersonInfoItem(Person person)
  {
    _person = person; 
  }

  public Name? FirstName => _person.Names.Where(n => (n.Type & NameType.FirstName) == NameType.FirstName).FirstOrDefault();
  public Name? MiddleName => _person.Names.Where(n => (n.Type & NameType.MiddleName) == NameType.MiddleName).FirstOrDefault();
  public Name? LastName => _person.Names.Where(n => (n.Type & NameType.LastName)== NameType.LastName).FirstOrDefault();
}
