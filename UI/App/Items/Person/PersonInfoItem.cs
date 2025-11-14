using GT4.Core.Project.Dto;
using GT4.UI.Formatters;

namespace GT4.UI.Items;

public class PersonInfoItem : CollectionItemBase<PersonInfo>
{
  private readonly INameFormatter _NameFormatter;

  public PersonInfoItem(PersonInfo personInfo, INameFormatter nameFormatter)
    : base(personInfo, personInfo.BiologicalSex == BiologicalSex.Female ? "female_stub.png" : "male_stub.png")
  {
    _NameFormatter = nameFormatter;
  }

  protected override ImageSource? CustomImage => Info.MainPhoto is null ? null : ImageUtils.ImageFromBytes(Info.MainPhoto.Content);

  public string CommonName => _NameFormatter.GetCommonPersonName(Info);
}
