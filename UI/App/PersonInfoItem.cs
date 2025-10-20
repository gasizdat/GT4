using GT4.Core.Project;
using GT4.Core.Project.Dto;

namespace GT4.UI;

public class PersonInfoItem
{
  private readonly Person _person;
  private readonly INameFormatter _nameFormatter;

  public PersonInfoItem(Person person, INameFormatter nameFormatter)
  {
    _person = person; 
    _nameFormatter = nameFormatter;
  }

  public string CommonName => _nameFormatter.GetCommonPersonName(_person);
}
