using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;

namespace GT4.UI.App.Dialogs;

public partial class SelectNameDialog : ContentPage
{
  public record NameTypeInfoItem(string TypeName, NameType Type);

  private readonly TaskCompletionSource<Name?> _Info = new(null);
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ObservableCollection<NameTypeInfoItem> _NameTypes;
  private NameTypeInfoItem _CurrentNameType;
  private readonly NameType _NameDeclension;
  private NameInfoItem? _CurrentName;

  private bool _NotReady => _CurrentName is null;


  public SelectNameDialog(BiologicalSex biologicalSex, ServiceProvider serviceProvider)
  {
    _NameTypeFormatter = serviceProvider.GetRequiredService<INameTypeFormatter>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _NameTypes = new((new[] { NameType.FirstName, NameType.MiddleName, NameType.LastName, NameType.AdditionalName })
      .Select(type => new NameTypeInfoItem(_NameTypeFormatter.ToString(type), type)));
    _CurrentNameType = _NameTypes.First();

    switch (biologicalSex)
    {
      case BiologicalSex.Male:
        _NameDeclension = NameType.MaleDeclension;
        break;
      case BiologicalSex.Female:
        _NameDeclension = NameType.FemaleDeclension;
        break;
      default:
        _NameDeclension = NameType.AllNames;
        break;
    }

    InitializeComponent();
  }

  public ICollection<NameTypeInfoItem> NameTypes => _NameTypes;

  public ICollection<NameInfoItem> Names =>
    _CurrentProjectProvider
    .Project
    .Names
    .GetNamesAsync(CurrentNameType.Type | _NameDeclension, _CancellationTokenProvider.CreateDbCancellationToken())
    .Result
    .Values
    .Select(name => new NameInfoItem(name, _NameTypeFormatter))
    .ToArray();

  public string SelectNameBtnName => _NotReady ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;

  public Task<Name?> Name => _Info.Task;

  public NameTypeInfoItem CurrentNameType
  {
    get => _CurrentNameType;
    set
    {
      if (_CurrentNameType == value)
        return;

      _CurrentNameType = value;
      OnPropertyChanged(nameof(Names));
      CurrentName = null;
    }
  }

  public NameInfoItem? CurrentName
  {
    get => _CurrentName;
    set
    {
      _CurrentName = value;
      OnPropertyChanged(nameof(SelectNameBtnName));
    }
  }

  public async void OnAddNameBtn(object sender, EventArgs e)
  {
    NameType dialogNameType;
    switch (_CurrentNameType.Type)
    {
      case NameType.MiddleName:
        dialogNameType = NameType.FirstName | NameType.MaleDeclension;
        break;
      case NameType.FirstName:
        dialogNameType = NameType.FirstName | _NameDeclension;
        break;
      case NameType.LastName:
        dialogNameType = NameType.FamilyName;
        break;
      case NameType.AdditionalName:
      default:
        return;
    }

    var dialog = new CreateNewNameDialog(dialogNameType);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
      return;

    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      Name name;
      switch (dialogNameType)
      {
        case NameType.FamilyName:
          name = await project
            .Family
            .AddFamilyAsync(familyName: info.Name, maleLastName: info.MaleName, femaleLastName: info.FemaleName, token);
          break;

        case NameType.FirstName | NameType.MaleDeclension:
          name = await project.Names.AddFirstMaleNameAsync(firstName: info.Name, maleMiddleName: info.MaleName, femaleMiddleName: info.FemaleName, token);
          break;
        case NameType.FirstName | NameType.FemaleDeclension:
          name = await project.Names.AddFirstFemaleNameAsync(info.Name, token);
          break;
        default:
          throw new ApplicationException(nameof(OnAddNameBtn));
      }
      OnPropertyChanged(nameof(Names));

      // TODO select created name
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
  }

  public void OnSelectNameBtn(object sender, EventArgs e)
  {
    _Info.SetResult(_NotReady ? null : _CurrentName?.Info);
  }
}