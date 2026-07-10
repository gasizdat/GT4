using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(FamilyName), "FamilyName")]
public partial class FamilyPage : ContentPage
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  private readonly ObservableCollection<PersonInfo> _Persons = new();
  private bool _PersonsLoaded;
  private Name? _FamilyName = null;
  private double _PersonItemMinimalWidth;

  public FamilyPage(
    IServiceProvider serviceProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    [FromKeyedServices(NameFormat.ShortPersonName)]
    IComparer<PersonInfo>? personInfoComparerByShortNames,
    IComparer<PersonInfo> personInfoComparer,
    IAlertService alertService,
    INavigationService navigationService
    )
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _PersonInfoComparer = personInfoComparerByShortNames ?? personInfoComparer;
    _AlertService = alertService;
    _NavigationService = navigationService;

    MemberItemTappedCommand = new SafeCommand<PersonInfo>(OnOpenPerson, _AlertService);
    PageCommand = new SafeCommand(OnPageCommand, _AlertService);

    InitializeComponent();
  }

  public Name? FamilyName
  {
    get => _FamilyName;
    set
    {
      _FamilyName = value;
      _PersonsLoaded = false;
      OnPropertyChanged(nameof(Persons));
      OnPropertyChanged(nameof(FamilyName));
      OnPropertyChanged(nameof(RemoveFamilyToolbarItemName));
      OnPropertyChanged(nameof(EditFamilyToolbarItemName));
    }
  }

  public double PersonItemMinimalWidth => _PersonItemMinimalWidth;

  public ICommand MemberItemTappedCommand { get; init; }

  public ICommand PageCommand { get; init; }

  public NameFormat PersonNamesFormat => NameFormat.ShortPersonName;

  public ICollection<PersonInfo> Persons
  {
    get
    {
      if (FamilyName is null)
      {
        return _Persons;
      }
      var familyName = FamilyName;

      async Task ListPersonsAsync(Name familyName)
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var persons = await _CurrentProjectProvider
          .Project
          .PersonManager
          .GetPersonInfosByNameAsync(name: familyName, selectMainPhoto: true, token);

        persons = [.. persons.OrderBy(item => item, _PersonInfoComparer)];
        await SafeTask.RunOnMainThread(() =>
        {
          _Persons.Clear();
          foreach (var person in persons)
          {
            _Persons.Add(person);
          }
        }, _AlertService);
      }

      if (!_PersonsLoaded)
      {
        _PersonsLoaded = true;
        SafeTask.Run(() => ListPersonsAsync(familyName), _AlertService);
      }

      return _Persons;
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
       await _AlertService.ShowConfirmationAsync(
         string.Format(UIStrings.AlertTextDeleteConfirmationText_1, _FamilyName.Value));

    if (!canDelete)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider
      .Project
      .FamilyManager
      .RemoveFamilyAsync(_FamilyName!, token);

    await _NavigationService.GoToAsync("..", true);
  }

  private async Task OnCreatePerson()
  {
    var dialog = new CreateOrUpdatePersonDialog(null, _ServiceProvider);

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

    var newPerson = await _CurrentProjectProvider
      .Project
      .PersonManager
      .AddPersonAsync(person, token);

    var insertAt = _Persons.TakeWhile(p => _PersonInfoComparer.Compare(p, newPerson) <= 0).Count();
    _Persons.Insert(insertAt, newPerson);
  }

  protected async Task OnOpenPerson(PersonInfo familyMember)
  {
    await _NavigationService.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = familyMember });
  }

  protected async Task OnPageCommand(object parameter)
  {
    switch (parameter)
    {
      case string commandName when commandName == "RemoveFamily":
        await OnDeleteFamily();
        break;

      case string commandName when commandName == "EditFamily":
        await CreateOrUpdateNameDialog.UpdateNameAsync(
          FamilyName!,
          _CurrentProjectProvider,
          _CancellationTokenProvider,
          _ServiceProvider.GetRequiredService<INameTypeFormatter>(),
          Navigation);
        break;

      case string commandName when commandName == "CreatePerson":
        await OnCreatePerson();
        break;

      case string commandName when commandName == "Refresh":
        this.RefreshView();
        break;
    }
  }
}
