using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Items;
using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewPersonDialog : ContentPage
{
  private readonly string _SaveButtonName;
  private readonly List<ImageSource> _Photos = new();
  private readonly List<Name> _Names = new();
  private readonly List<RelativeMemberInfoItem> _Relatives = new();
  private readonly TaskCompletionSource<Person?> _Person = new(null);
  private Date _BirthDate;
  private Date? _DeathDate;
  private BiologicalSex _Sex;
  private bool _NotReady = true;

  public CreateNewPersonDialog(Person? person)
  {
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;

    // TODO just testing
    _Photos.Add(ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync("female_stub.png")));
    _Photos.Add(ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync("male_stub.png")));

    // TODO just testing
    _Names.Add(new Name(0, "Clark", NameType.FirstName, null));
    _Names.Add(new Name(0, "Jeremy", NameType.AdditionalName, null));
    _Names.Add(new Name(0, "Campbell", NameType.LastName, null));

    // TODO just testing
    _BirthDate = Date.Create(20251029, DateStatus.WellKnown);

    // TODO relatives just testing
    _Relatives.Add(new RelativeMemberInfoItem(new Relative(
      new Person(0, [new Name(0, "Мариванна", NameType.FirstName, 0)], null, Date.Create(19900000, DateStatus.YearApproximate), null, BiologicalSex.Female), 
      RelationshipType.Mother, Date.Create(20050521, DateStatus.WellKnown)), ServiceBuilder.DefaultServices));
    _Relatives.Add(new RelativeMemberInfoItem(new Relative(
      new Person(0, [new Name(0, "Скуфовский", NameType.LastName, 0)], null, Date.Create(19951127, DateStatus.DayUnknown), null, BiologicalSex.Male), 
      RelationshipType.Father, Date.Create(19850521, DateStatus.YearApproximate)), ServiceBuilder.DefaultServices));

    InitializeComponent();
    BindingContext = this;
  }

  public ICollection<ImageSource> Photos => _Photos;

  public ICollection<Name> Names => _Names;

  public ICollection<RelativeMemberInfoItem> Relatives => _Relatives;

  public Date BirthDate
  {
    get => _BirthDate;
    set
    {
      _BirthDate = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Date? DeathDate
  {
    get => _DeathDate;
    set
    {
      _DeathDate = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public BiologicalSex Sex
  {
    get => _Sex;
    set
    {
      _Sex = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Task<Person?> Person => _Person.Task;
  public string CreatePersonBtnName => _NotReady ? UIStrings.BtnNameCancel : _SaveButtonName;

  public void OnCreatePersonBtn(object sender, EventArgs e)
  {
    // TODO
    _Person.SetResult(null);
  }
}