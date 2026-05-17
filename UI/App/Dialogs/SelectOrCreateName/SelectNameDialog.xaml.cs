using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class SelectNameDialog : ContentPage
{
  private readonly TaskCompletionSource<Name?> _Info = new(null);
  private readonly IServiceProvider _ServiceProvider;
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IComparer<Name> _NameComparer;
  private readonly ObservableCollection<NameTypeInfoItem> _NameTypes;
  private readonly ICommand _DialogCommand;
  private ICollection<NameInfoItem>? _Names;
  private NameTypeInfoItem _CurrentNameType;
  private readonly NameType _NameDeclension;
  private NameInfoItem? _CurrentName;

  private bool _NotReady => _CurrentName is null;

  public SelectNameDialog(BiologicalSex biologicalSex, IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _NameTypeFormatter = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _NameComparer = _ServiceProvider.GetRequiredService<IComparer<Name>>();
    _NameTypes = new((new[] { NameType.FirstName, NameType.Patronymic, NameType.LastName })
      .Select(type => new NameTypeInfoItem(_NameTypeFormatter.ToString(type), type)));
    _DialogCommand = new SafeCommand(OnDialogCommandAsync);
    _CurrentNameType = _NameTypes.First();

    _NameDeclension = biologicalSex switch
    {
      BiologicalSex.Male => NameType.MaleDeclension,
      BiologicalSex.Female => NameType.FemaleDeclension,
      _ => NameType.AllNames
    };

    InitializeComponent();
  }

  private async Task OnDialogCommandAsync(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "AddName":
        await OnAddNameAsync();
        break;
      case string commandName when commandName == "SelectName":
        OnSelectName();
        break;
      case Name nameInfo:
        await CreateOrUpdateNameDialog.UpdateNameAsync(nameInfo, _ServiceProvider, Navigation);
        Names = null;
        CurrentName = Names?.SingleOrDefault(n => n.Info.Id == nameInfo.Id);
        break;
    }
  }

  public ICollection<NameTypeInfoItem> NameTypes => _NameTypes;

  public ICommand DialogCommand => _DialogCommand;

  public ICollection<NameInfoItem>? Names
  {
    get
    {
      if (_Names != null)
      {
        return _Names;
      }

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      _Names = _CurrentProjectProvider
        .Project
        .Names
        .GetNamesByTypeAsync(CurrentNameType.Type | _NameDeclension, token)
        .Result
        .Select(name => new NameInfoItem(name, _NameTypeFormatter))
        .OrderBy(name => name.Info, _NameComparer)
        .ToArray();

      return _Names;
    }
    set
    {
      _Names = value;
      OnPropertyChanged(nameof(Names));
    }
  }

  public string DialogButtonName => _NotReady ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;

  public Task<Name?> Name => _Info.Task;

  public NameTypeInfoItem CurrentNameType
  {
    get => _CurrentNameType;
    set
    {
      if (_CurrentNameType == value)
        return;

      _CurrentNameType = value;
      Names = null;
      CurrentName = null;
    }
  }

  public NameInfoItem? CurrentName
  {
    get => _CurrentName;
    set
    {
      if (value?.Info.Id != _CurrentName?.Info.Id)
      {
        _CurrentName = value;

        OnPropertyChanged(nameof(CurrentName));
        OnPropertyChanged(nameof(DialogButtonName));
      }
    }
  }

  public async Task OnAddNameAsync()
  {
    NameType dialogNameType;
    switch (_CurrentNameType.Type)
    {
      case NameType.Patronymic:
        dialogNameType = NameType.FirstName | NameType.MaleDeclension;
        break;
      case NameType.FirstName:
        dialogNameType = NameType.FirstName | _NameDeclension;
        break;
      case NameType.LastName:
        dialogNameType = NameType.FamilyName;
        break;
      default:
        return;
    }

    var dialog = new CreateOrUpdateNameDialog(dialogNameType, _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
      return;

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var project = _CurrentProjectProvider.Project;
    var name = dialogNameType switch
    {
      NameType.FamilyName =>
        await project
          .FamilyManager
          .AddFamilyAsync(familyName: info.Name, maleLastName: info.MaleName, femaleLastName: info.FemaleName, token),

      NameType.FirstName | NameType.MaleDeclension =>
        await project.Names.AddFirstMaleNameAsync(firstName: info.Name, malePatronymic: info.MaleName, femalePatronymic: info.FemaleName, token),

      NameType.FirstName | NameType.FemaleDeclension =>
        await project.Names.AddFirstFemaleNameAsync(info.Name, token),

      _ => throw new ApplicationException(nameof(OnAddNameAsync))
    };
    Names = null;
    CurrentName = Names?.SingleOrDefault(n => n.Info.Id == name.Id);
  }

  public void OnSelectName()
  {
    _Info.SetResult(_NotReady ? null : _CurrentName?.Info);
  }
}
