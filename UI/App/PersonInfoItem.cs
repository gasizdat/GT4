using GT4.Core.Project;
using GT4.Core.Project.Dto;

namespace GT4.UI;

public class PersonInfoItem
{
  private readonly Person _person;
  private readonly INameFormatter _nameFormatter;
  private Task<Stream> DefaultImage
  {
    get
    {
      var resourceName = _person.BiologicalSex == BiologicalSex.Female ? "female_stub.png" : "male_stub.png";
      return FileSystem.OpenAppPackageFileAsync(resourceName);
    }
  }

  public PersonInfoItem(Person person, INameFormatter nameFormatter)
  {
    _person = person;
    _nameFormatter = nameFormatter;
  }

  public string CommonName => _nameFormatter.GetCommonPersonName(_person);
  public ImageSource MainImage => ImageSource.FromStream(token => _person.MainPhoto is null ? 
    DefaultImage : Task.Run<Stream>(() => new MemoryStream(_person.MainPhoto), token));
}
