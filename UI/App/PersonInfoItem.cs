using GT4.Core.Project;
using GT4.Core.Project.Dto;

namespace GT4.UI;

public class PersonInfoItem
{
  private readonly Person _Person;
  private readonly INameFormatter _NameFormatter;
  private Task<Stream> _DefaultImage
  {
    get
    {
      var resourceName = _Person.BiologicalSex == BiologicalSex.Female ? "female_stub.png" : "male_stub.png";
      return FileSystem.OpenAppPackageFileAsync(resourceName);
    }
  }

  public PersonInfoItem(Person person, INameFormatter nameFormatter)
  {
    _Person = person;
    _NameFormatter = nameFormatter;
  }

  public Person Person => _Person;
  public string CommonName => _NameFormatter.GetCommonPersonName(_Person);
  public ImageSource MainImage => ImageSource.FromStream(token => _Person.MainPhoto is null ? 
    _DefaultImage : Task.Run<Stream>(() => new MemoryStream(_Person.MainPhoto), token));
}
