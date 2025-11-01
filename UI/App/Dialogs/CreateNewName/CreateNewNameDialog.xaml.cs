using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewNameDialog : ContentPage
{
  public record FamilyInfo(string Name, string MaleLastName, string FemaleLastName);

  private readonly NameType _NameType;
  private readonly TaskCompletionSource<FamilyInfo?> _Info = new(null);
  private string _GeneralName = string.Empty;
  private string _MaleName = string.Empty;
  private string _FemaleName = string.Empty;
  private bool _NotReady =>
    string.IsNullOrWhiteSpace(_GeneralName) ||
    ShowDeclensionNames && (string.IsNullOrWhiteSpace(_MaleName) || string.IsNullOrWhiteSpace(_FemaleName));

  public CreateNewNameDialog(NameType nameType)
  {
    switch (nameType)
    {
      case NameType.FamilyName:
      case NameType.FirstName | NameType.MaleDeclension:
      case NameType.FirstName | NameType.FemaleDeclension:
        _NameType = nameType;
        break;

      default:
        throw new NotSupportedException($"Unsupported name type {nameType}");
    }


    InitializeComponent();
  }

  public bool ShowDeclensionNames =>
    _NameType == NameType.FamilyName || _NameType == (NameType.FirstName | NameType.MaleDeclension);

  public string GeneralName
  {
    get => _GeneralName;
    set
    {
      _GeneralName = value;
      OnPropertyChanged(nameof(GeneralName));
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public string MaleName
  {
    get => _MaleName;
    set
    {
      _MaleName = value;
      OnPropertyChanged(nameof(MaleName));
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public string FemaleName
  {
    get => _FemaleName;
    set
    {
      _FemaleName = value;
      OnPropertyChanged(nameof(FemaleName));
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }

  public string GeneralNameDescription
  {
    get
    {
      switch (_NameType)
      {
        case NameType.FamilyName:
          return UIStrings.FieldFamilyNameEntry;
        case NameType.FirstName | NameType.MaleDeclension:
          return UIStrings.FieldFirstNameMaleEntry;
        case NameType.FirstName | NameType.FemaleDeclension:
          return UIStrings.FieldFirstNameFemaleEntry;
        default:
          throw new ApplicationException(nameof(GeneralNameDescription));
      }
    }
  }

  public string GeneralNamePlaceholder
  {
    get
    {
      switch (_NameType)
      {
        case NameType.FamilyName:
          return UIStrings.TxtPlaceholderFamilyName;
        case NameType.FirstName | NameType.MaleDeclension:
          return UIStrings.TxtPlaceholderMaleName;
        case NameType.FirstName | NameType.FemaleDeclension:
          return UIStrings.TxtPlaceholderFemaleName;
        default:
          throw new ApplicationException(nameof(GeneralNamePlaceholder));
      }
    }
  }

  public string MaleNameDescription
  {
    get
    {
      switch (_NameType)
      {
        case NameType.FamilyName:
          return UIStrings.FieldLastNameMaleEntry;
        case NameType.FirstName | NameType.MaleDeclension:
          return UIStrings.FieldMiddleNameMaleEntry;
        case NameType.FirstName | NameType.FemaleDeclension:
          return string.Empty;
        default:
          throw new ApplicationException(nameof(MaleNameDescription));
      }
    }
  }

  public string MaleNamePlaceholder
  {
    get
    {
      switch (_NameType)
      {
        case NameType.FamilyName:
          return UIStrings.TxtPlaceholderLastNameMale;
        case NameType.FirstName | NameType.MaleDeclension:
          return UIStrings.TxtPlaceholderMiddleNameMale;
        case NameType.FirstName | NameType.FemaleDeclension:
          return string.Empty;
        default:
          throw new ApplicationException(nameof(MaleNamePlaceholder));
      }
    }
  }

  public string FemaleNameDescription
  {
    get
    {
      switch (_NameType)
      {
        case NameType.FamilyName:
          return UIStrings.FieldLastNameFemaleEntry;
        case NameType.FirstName | NameType.MaleDeclension:
          return UIStrings.FieldMiddleNameFemaleEntry;
        case NameType.FirstName | NameType.FemaleDeclension:
          return string.Empty;
        default:
          throw new ApplicationException(nameof(FemaleNameDescription));
      }
    }
  }

  public string FemaleNamePlaceholder
  {
    get
    {
      switch (_NameType)
      {
        case NameType.FamilyName:
          return UIStrings.TxtPlaceholderLastNameFemale;
        case NameType.FirstName | NameType.MaleDeclension:
          return UIStrings.TxtPlaceholderMiddleNameFemale;
        case NameType.FirstName | NameType.FemaleDeclension:
          return string.Empty;
        default:
          throw new ApplicationException(nameof(FemaleNamePlaceholder));
      }
    }
  }

  public Task<FamilyInfo?> Info => _Info.Task;
  public string CreateFamilyBtnName => _NotReady ? UIStrings.BtnNameCancel : UIStrings.BtnNameCreateFamily;

  public void OnCreateFamilyBtn(object sender, EventArgs e)
  {
    _Info.SetResult(_NotReady ? null : new(GeneralName, ShowDeclensionNames ? MaleName : string.Empty, ShowDeclensionNames ? FemaleName : string.Empty));
  }
}