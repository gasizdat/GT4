using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewPersonDialog : ContentPage
{
  private readonly string _SaveButtonName;
  private bool _NotReady = true;
  private readonly List<ImageSource> _Photos = new();
  private Name? _FirstName;
  private Name? _MiddleName;
  private Name? _LastName;
  private Name[]? _AdditionalNames;
  private Date _BirthDate;
  private Date? _DeathDate;
  private BiologicalSex _Sex;

  public CreateNewPersonDialog(Person? person)
  {
    InitializeComponent();
    BindingContext = this;
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;

    // TODO just tests
    _Photos.Add(ImageSource.FromFile("family_stub.png"));
    _Photos.Add(ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync("family_stub.png")));
  }

  public ICollection<ImageSource> Photos => _Photos;

  public Name? FirstName
  {
    get => _FirstName;
    set
    {
      _FirstName = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Name? MiddleName
  {
    get => _MiddleName;
    set
    {
      _MiddleName = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Name? LastName
  {
    get => _LastName;
    set
    {
      _LastName = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Name[]? AdditionalNames
  {
    get => _AdditionalNames;
    set
    {
      _AdditionalNames = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

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