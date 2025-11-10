using GT4.Core.Project.Dto;

namespace GT4.UI.App.Items;

public class PersonInfoItem : CollectionItemBase<Person>
{
  private readonly INameFormatter _NameFormatter;

  public PersonInfoItem(Person person, INameFormatter nameFormatter)
    : base(person, person.BiologicalSex == BiologicalSex.Female ? "female_stub.png" : "male_stub.png")
  {
    _NameFormatter = nameFormatter;
  }

  protected override ImageSource? CustomImage => Info.MainPhoto is null ? null : ImageUtils.ImageFromBytes(Info.MainPhoto.Content);

  public string CommonName => _NameFormatter.GetCommonPersonName(Info);
}
