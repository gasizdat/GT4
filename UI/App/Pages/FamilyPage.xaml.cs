using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(FamilyName), "FamilyName")]
public partial class FamilyPage : ContentPage
{
  private readonly IServiceProvider _Services;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private Name? _FamilyName = null;
  private double _PersonItemMinimalWidth;

  protected FamilyPage(IServiceProvider serviceProvider)
  {
    _Services = serviceProvider;
    _CancellationTokenProvider = _Services.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = _Services.GetRequiredService<ICurrentProjectProvider>();
    _PersonInfoComparer = _Services.GetRequiredService<IComparer<PersonInfo>>();

    MemberItemTappedCommand = new Command<PersonInfo>(OnOpenPerson);
    PageCommand = new Command<object>(OnPageCommand);

    InitializeComponent();
  }

  public FamilyPage()
    : this(ServiceBuilder.DefaultServices)
  {
  }

  public Name? FamilyName
  {
    get => _FamilyName;
    set
    {
      _FamilyName = value;
      OnPropertyChanged(nameof(Persons));
      OnPropertyChanged(nameof(FamilyName));
      OnPropertyChanged(nameof(RemoveFamilyToolbarItemName));
      OnPropertyChanged(nameof(EditFamilyToolbarItemName));
    }
  }

  public double PersonItemMinimalWidth => _PersonItemMinimalWidth;

  public ICommand MemberItemTappedCommand { get; init; }

  public ICommand PageCommand { get; init; }

  public ICollection<PersonInfo> Persons
  {
    get
    {
      if (FamilyName is null)
      {
        return [];
      }

      try
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var ret = _CurrentProjectProvider
          .Project
          .PersonManager
          .GetPersonInfosByNameAsync(name: FamilyName, selectMainPhoto: true, token)
          .Result
          .OrderBy(item => item, _PersonInfoComparer)
          .ToList();

        return ret;
      }
      catch (Exception ex)
      {
        _ = PageAlert.ShowError(ex);
        return Enumerable.Empty<PersonInfo>().ToList();
      }
    }
  }

  public string RemoveFamilyToolbarItemName =>
    string.Format(UIStrings.MenuItemNameRemove_1, _FamilyName?.Value ?? string.Empty);

  public string EditFamilyToolbarItemName =>
    string.Format(UIStrings.MenuItemNameEdit_1, _FamilyName?.Value ?? string.Empty);

  protected override void OnSizeAllocated(double width, double height)
  {
    const double PercentageOfWidth = 0.9;
    const int ItemsPerRow = 2;

    base.OnSizeAllocated(width, height);
    _PersonItemMinimalWidth = width * PercentageOfWidth / ItemsPerRow;

    OnPropertyChanged(nameof(PersonItemMinimalWidth));
  }

  private async Task OnDeleteFamily()
  {
    var canDelete = _FamilyName is not null &&
       await this.ShowConfirmation(string.Format(UIStrings.AlertTextDeleteConfirmationText_1, _FamilyName.Value));

    if (!canDelete)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider
      .Project
      .FamilyManager
      .RemoveFamilyAsync(_FamilyName!, token);

    await Shell.Current.GoToAsync("..", true);
  }

  private async Task OnEditFamily()
  {
    Name[]? names;
    using (var token = _CancellationTokenProvider.CreateDbCancellationToken())
    {
      names = await _CurrentProjectProvider
        .Project
        .Names
        .TryGetNameWithSubnamesByIdAsync(FamilyName?.Id, token);
    }

    var familyName = names?.SingleOrDefault(n => n.Type.HasFlag(NameType.FamilyName));
    var maleLastName = names?.SingleOrDefault(n => n.Type.HasFlag(NameType.LastName | NameType.MaleDeclension));
    var femaleLastName = names?.SingleOrDefault(n => n.Type.HasFlag(NameType.LastName | NameType.FemaleDeclension));
    if (familyName is null)
    {
      return;
    }

    var dialog = new CreateOrUpdateNameDialog(familyName, maleLastName, femaleLastName, _Services);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
    {
      return;
    }

    familyName = familyName with { Value = info.Name };
    maleLastName = maleLastName is null ? null : maleLastName with { Value = info.MaleName };
    femaleLastName = femaleLastName is null ? null : femaleLastName with { Value = info.FemaleName };

    using (var token = _CancellationTokenProvider.CreateDbCancellationToken())
    {
      await _CurrentProjectProvider
        .Project
        .FamilyManager
        .UpdateFamilyAsync(familyName, maleLastName, femaleLastName, token);
    }

    FamilyName = familyName;
  }

  private async Task OnCreatePerson()
  {
    var dialog = new CreateOrUpdatePersonDialog(null, _Services);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null || _FamilyName is null)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var person = _CurrentProjectProvider
      .Project
      .FamilyManager
      .SetUpPersonFamily(info, _FamilyName);

    await _CurrentProjectProvider
      .Project
      .PersonManager
      .AddPersonAsync(person, token);

    OnPropertyChanged(nameof(Persons));
  }

  private async void OnOpenPerson(PersonInfo familyMember)
  {
    await Shell.Current.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = familyMember });
  }

  private async void OnPageCommand(object parameter)
  {
    try
    {
      switch (parameter)
      {
        case string commandName when commandName == "RemoveFamily":
          await OnDeleteFamily();
          break;

        case string commandName when commandName == "EditFamily":
          await OnEditFamily();
          break;

        case string commandName when commandName == "CreatePerson":
          await OnCreatePerson();
          break;

        case string commandName when commandName == "Refresh":
          Utils.RefreshView(this);
          break;
      }
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
  }
}
