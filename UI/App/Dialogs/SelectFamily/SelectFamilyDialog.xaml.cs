using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class SelectFamilyDialog : ContentPage
{
  private readonly TaskCompletionSource<Name?> _Info = new(null);
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IComparer<Name> _NameComparer;
  private readonly ICommand _DialogCommand;
  private Name[] _AllFamilies;
  private string _FilterText = string.Empty;
  private Name? _CurrentFamily;

  public SelectFamilyDialog(Name[] families, IServiceProvider serviceProvider)
  {
    _NameTypeFormatter = serviceProvider.GetRequiredService<INameTypeFormatter>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _NameComparer = serviceProvider.GetRequiredService<IComparer<Name>>();
    var alertService = serviceProvider.GetRequiredService<IAlertService>();
    _DialogCommand = new SafeCommand(OnDialogCommandAsync, alertService);
    _AllFamilies = [.. families.OrderBy(family => family, _NameComparer)];

    InitializeComponent();
  }

  private async Task OnDialogCommandAsync(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "AddFamily":
        await OnAddFamilyAsync();
        break;
      case string commandName when commandName == "SelectFamily":
        OnSelectFamily();
        break;
    }
  }

  public ICommand DialogCommand => _DialogCommand;

  public Name[] Families => [.. _AllFamilies.Where(family => WildcardMatcher.IsMatch(family.Value, _FilterText))];

  public string FilterText
  {
    get => _FilterText;
    set
    {
      if (_FilterText == value)
        return;

      _FilterText = value;

      OnPropertyChanged(nameof(FilterText));
      OnPropertyChanged(nameof(Families));
    }
  }

  public Name? CurrentFamily
  {
    get => _CurrentFamily;
    set
    {
      if (_CurrentFamily?.Id != value?.Id)
      {
        _CurrentFamily = value;

        OnPropertyChanged(nameof(CurrentFamily));
        OnPropertyChanged(nameof(DialogButtonName));
      }
    }
  }

  public string DialogButtonName => _CurrentFamily is null ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;

  public Task<Name?> Family => _Info.Task;

  public void OnSelectFamily()
  {
    _Info.SetResult(_CurrentFamily);
  }

  public async Task OnAddFamilyAsync()
  {
    var dialog = new CreateOrUpdateNameDialog(NameType.FamilyName, _NameTypeFormatter);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
      return;

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var newFamily = await _CurrentProjectProvider
      .Project
      .FamilyManager
      .AddFamilyAsync(info.Name, info.MaleName, info.FemaleName, token);

    _AllFamilies = [.. _AllFamilies, newFamily];
    FilterText = string.Empty;
    CurrentFamily = newFamily;
    OnPropertyChanged(nameof(Families));
  }
}
