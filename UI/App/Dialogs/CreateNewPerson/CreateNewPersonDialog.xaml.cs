using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewPersonDialog : ContentPage
{
  private readonly string _SaveButtonName;
  private bool _NotReady = true;
  private readonly List<ImageSource> _Photos = new();
  private readonly List<Name> _Names = new();
  private Date _BirthDate;
  private Date? _DeathDate;
  private BiologicalSex _Sex;

  public CreateNewPersonDialog(Person? person)
  {
    InitializeComponent();
    BindingContext = this;
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;

    // TODO just tests
    _Photos.Add(ImageSource.FromFile("female_stub.png"));
    _Photos.Add(ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync("male_stub.png")));

    // TODO just tests
    _Names.Add(new Name(0, "Clark", NameType.FirstName, null));
    _Names.Add(new Name(0, "Jeremy", NameType.AdditionalName, null));
    _Names.Add(new Name(0, "Campbell", NameType.FirstName, null));
  }

  public ICollection<ImageSource> Photos => _Photos;

  public ICollection<Name> Names => _Names;

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

  public Task<Person?> Person => new TaskCompletionSource<Person?>(null).Task;
  public string CreatePersonBtnName => _NotReady ? UIStrings.BtnNameCancel : _SaveButtonName;
}