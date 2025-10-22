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
  public ImageSource MainImage
  {
    get
    {
      var resourceName = _person.BiologicalSex == BiologicalSex.Female ? "female_stub.png" : "male_stub.png";
      var ret = ImageSource.FromStream(token => FileSystem.OpenAppPackageFileAsync(resourceName));
      return ret;
    }
  }
}
