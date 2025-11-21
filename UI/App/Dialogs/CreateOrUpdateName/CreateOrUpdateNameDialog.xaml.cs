using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Dialogs;

public partial class CreateOrUpdateNameDialog : ContentPage
{
  public record FamilyInfo(string Name, string MaleName, string FemaleName);

  private readonly NameType _NameType;
  private readonly TaskCompletionSource<FamilyInfo?> _Info = new(null);
  private string _GeneralName = string.Empty;
  private string _MaleName = string.Empty;
  private string _FemaleName = string.Empty;
  private bool _NotReady =>
    string.IsNullOrWhiteSpace(_GeneralName) ||
    ShowDeclensionNames && (string.IsNullOrWhiteSpace(_MaleName) || string.IsNullOrWhiteSpace(_FemaleName));

  public CreateOrUpdateNameDialog(NameType nameType)
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

  public CreateOrUpdateNameDialog(Name name, Name? maleName, Name? femaleName)
    : this(name.Type)
  {
    GeneralName = name.Value;
    MaleName = maleName?.Value ?? string.Empty;
    FemaleName = femaleName?.Value ?? string.Empty;
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

  public string GeneralNameDescription => _NameType switch
  {
    NameType.FamilyName => UIStrings.FieldFamilyNameEntry,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.FieldFirstNameMaleEntry,
    NameType.FirstName | NameType.FemaleDeclension => UIStrings.FieldFirstNameFemaleEntry,
    _ => throw new ApplicationException(nameof(GeneralNameDescription))
  };

  public string GeneralNamePlaceholder => _NameType switch
  {
    NameType.FamilyName => UIStrings.TxtPlaceholderFamilyName,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.TxtPlaceholderMaleName,
    NameType.FirstName | NameType.FemaleDeclension => UIStrings.TxtPlaceholderFemaleName,
    _ => throw new ApplicationException(nameof(GeneralNamePlaceholder))
  };

  public string MaleNameDescription => _NameType switch
  {
    NameType.FamilyName => UIStrings.FieldLastNameMaleEntry,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.FieldMiddleNameMaleEntry,
    NameType.FirstName | NameType.FemaleDeclension => string.Empty,
    _ => throw new ApplicationException(nameof(MaleNameDescription))
  };

  public string MaleNamePlaceholder => _NameType switch
  {
    NameType.FamilyName => UIStrings.TxtPlaceholderLastNameMale,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.TxtPlaceholderMiddleNameMale,
    NameType.FirstName | NameType.FemaleDeclension => string.Empty,
    _ => throw new ApplicationException(nameof(MaleNamePlaceholder))
  };

  public string FemaleNameDescription => _NameType switch
  {
    NameType.FamilyName => UIStrings.FieldLastNameFemaleEntry,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.FieldMiddleNameFemaleEntry,
    NameType.FirstName | NameType.FemaleDeclension => string.Empty,
    _ => throw new ApplicationException(nameof(FemaleNameDescription))
  };

  public string FemaleNamePlaceholder => _NameType switch
  {
    NameType.FamilyName => UIStrings.TxtPlaceholderLastNameFemale,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.TxtPlaceholderMiddleNameFemale,
    NameType.FirstName | NameType.FemaleDeclension => string.Empty,
    _ => throw new ApplicationException(nameof(FemaleNamePlaceholder))
  };

  public Task<FamilyInfo?> Info => _Info.Task;
  public string CreateFamilyBtnName => _NotReady ? UIStrings.BtnNameCancel : UIStrings.BtnNameCreateFamily;

  public void OnCreateFamilyBtn(object sender, EventArgs e)
  {
    _Info.SetResult(_NotReady ? null : new(GeneralName, ShowDeclensionNames ? MaleName : string.Empty, ShowDeclensionNames ? FemaleName : string.Empty));
  }
}