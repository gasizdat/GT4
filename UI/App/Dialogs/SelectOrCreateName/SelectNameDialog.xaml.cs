using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;

namespace GT4.UI.Dialogs;

public partial class SelectNameDialog : ContentPage
{
  public record NameTypeInfoItem(string TypeName, NameType Type);

  private readonly TaskCompletionSource<Name?> _Info = new(null);
  private readonly IServiceProvider _ServiceProvider;
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IComparer<NameInfoItem> _NameComparer;
  private readonly ObservableCollection<NameTypeInfoItem> _NameTypes;
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
    _NameComparer = _ServiceProvider.GetRequiredService<IComparer<NameInfoItem>>();
    _NameTypes = new((new[] { NameType.FirstName, NameType.MiddleName, NameType.LastName, NameType.AdditionalName })
      .Select(type => new NameTypeInfoItem(_NameTypeFormatter.ToString(type), type)));
    _CurrentNameType = _NameTypes.First();

    _NameDeclension = biologicalSex switch
    {
      BiologicalSex.Male => NameType.MaleDeclension,
      BiologicalSex.Female => NameType.FemaleDeclension,
      _ => NameType.AllNames
    };

    InitializeComponent();
  }

  public ICollection<NameTypeInfoItem> NameTypes => _NameTypes;

  public ICollection<NameInfoItem> Names
  {
    get
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var ret = _CurrentProjectProvider
        .Project
        .Names
        .GetNamesByTypeAsync(CurrentNameType.Type | _NameDeclension, token)
        .Result
        .Select(name => new NameInfoItem(name, _NameTypeFormatter))
        .OrderBy(name => name, _NameComparer)
        .ToArray();

      return ret;
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
      OnPropertyChanged(nameof(DialogButtonName));
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

    var dialog = new CreateOrUpdateNameDialog(dialogNameType, _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
      return;

    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      var name = dialogNameType switch
      {
        NameType.FamilyName =>
          await project
            .FamilyManager
            .AddFamilyAsync(familyName: info.Name, maleLastName: info.MaleName, femaleLastName: info.FemaleName, token),

        NameType.FirstName | NameType.MaleDeclension =>
          await project.Names.AddFirstMaleNameAsync(firstName: info.Name, maleMiddleName: info.MaleName, femaleMiddleName: info.FemaleName, token),

        NameType.FirstName | NameType.FemaleDeclension =>
          await project.Names.AddFirstFemaleNameAsync(info.Name, token),

        _ => throw new ApplicationException(nameof(OnAddNameBtn))
      };
      OnPropertyChanged(nameof(Names));

      // TODO select created name
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
  }

  public void OnSelectNameBtn(object sender, EventArgs e)
  {
    _Info.SetResult(_NotReady ? null : _CurrentName?.Info);
  }
}