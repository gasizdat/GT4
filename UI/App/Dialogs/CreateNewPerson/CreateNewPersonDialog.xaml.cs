using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewPersonDialog : ContentPage
{
  private readonly string _SaveButtonName;
  private bool _NotReady = true;
  private ImageSource? _MainPhoto;
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
  }

  public ImageSource? MainPhoto
  {
    get => _MainPhoto;
    set
    {
      _MainPhoto = value; 
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public Name? FirstName
  {
    get => _FirstName;
    set
    {
      _FirstName = value;
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public Name? MiddleName
  {
    get => _MiddleName;
    set
    {
      _MiddleName = value;
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public Name? LastName
  {
    get => _LastName;
    set
    {
      _LastName = value;
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public Name[]? AdditionalNames
  {
    get => _AdditionalNames;
    set
    {
      _AdditionalNames = value;
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public Date BirthDate
  {
    get => _BirthDate;
    set
    {
      _BirthDate = value;
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public Date? DeathDate
  {
    get => _DeathDate;
    set
    {
      _DeathDate = value;
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public BiologicalSex Sex
  {
    get => _Sex;
    set
    {
      _Sex = value;
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public Task<Person?> Person => new TaskCompletionSource<Person?>(null).Task;
  public string CreateFamilyBtnName => _NotReady ? UIStrings.BtnNameCancel : _SaveButtonName;
}