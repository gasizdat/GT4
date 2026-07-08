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
  private readonly FilteredObservableCollection<PersonInfo> _Persons = new();
  private readonly PersonInfoFilter _Filter;
  private bool _PersonsLoaded;
  private bool _FilterDataLoaded;
  private Name? _FamilyName = null;
  private double _PersonItemMinimalWidth;
  private bool _IsFiltersVisible;

  public FamilyPage(
    IServiceProvider serviceProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    [FromKeyedServices(NameFormat.ShortPersonName)]
    IComparer<PersonInfo>? personInfoComparerByShortNames,
    IComparer<PersonInfo> personInfoComparer,
    IAlertService alertService,
    INavigationService navigationService,
    IBiologicalSexFormatter biologicalSexFormatter
    )
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _PersonInfoComparer = personInfoComparerByShortNames ?? personInfoComparer;
    _AlertService = alertService;
    _NavigationService = navigationService;

    _Filter = new PersonInfoFilter(biologicalSexFormatter);
    _Filter.Changed += (_, _) => _Persons.Update();
    _Filter.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
    _Persons.Filter = PersonMatches;

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
      _FilterDataLoaded = false;
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

  public PersonInfoFilter Filter => _Filter;

  public bool IsAnyFilterActive => _Filter.IsAnyFilterActive;

  public bool IsFiltersVisible
  {
    get => _IsFiltersVisible;
    set
    {
      _IsFiltersVisible = value;
      OnPropertyChanged(nameof(IsFiltersVisible));
      OnPropertyChanged(nameof(ToggleFiltersButtonName));

      if (value)
      {
        EnsureFilterDataLoaded();
      }
    }
  }

  public string ToggleFiltersButtonName =>
    string.Format(UIStrings.BtnNameFilters_1, IsFiltersVisible ? "🔼" : "🔽");

  private bool PersonMatches(FilteredObservableCollection<PersonInfo> _, PersonInfo person) => _Filter.Matches(person);

  // Marital status needs a relatives lookup that no other part of this page's data requires, so it
  // is fetched lazily -- only once the filter panel is actually opened -- reusing the persons already
  // loaded into _Persons (snapshotted here, on the main thread, before handing off to the background
  // fetch) rather than fetching it unconditionally for every visit to this page.
  private void EnsureFilterDataLoaded()
  {
    if (_FilterDataLoaded)
    {
      return;
    }
    _FilterDataLoaded = true;

    // Snapshot to a detached array now (AllItems exposes the live internal list) so a later mutation
    // -- e.g. OnCreatePerson -- can't change what this fetch is computed against mid-flight.
    var allPersons = _Persons.AllItems.ToArray();

    async Task LoadFilterDataAsync()
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var relatives = await _CurrentProjectProvider.Project.Relatives.GetRelativesForPersonsAsync(allPersons, token);
      var marriedIds = relatives
        .Where(kv => kv.Value.Any(r => r.Type == RelationshipType.Spouse))
        .Select(kv => kv.Key)
        .ToArray();
      var (minYear, maxYear) = PersonInfoFilter.ComputeYearBounds(allPersons);

      await SafeTask.RunOnMainThread(() =>
      {
        _Filter.SetMarriedIds(marriedIds);
        _Filter.SetYearBounds(minYear, maxYear);
      }, _AlertService);
    }

    SafeTask.Run(LoadFilterDataAsync, _AlertService);
  }

  public ICollection<PersonInfo> Persons
  {
    get
    {
      if (FamilyName is null)
      {
        return _Persons.Items;
      }
      var familyName = FamilyName;

      async Task ListPersonsAsync(Name familyName)
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var project = _CurrentProjectProvider.Project;
        var persons = await project
          .PersonManager
          .GetPersonInfosByNameAsync(name: familyName, selectMainPhoto: true, token);

        persons = [.. persons.OrderBy(item => item, _PersonInfoComparer)];

        await SafeTask.RunOnMainThread(() =>
        {
          _Persons.Clear();
          _Persons.AddRange(persons);
        }, _AlertService);
      }

      if (!_PersonsLoaded)
      {
        _PersonsLoaded = true;
        SafeTask.Run(() => ListPersonsAsync(familyName), _AlertService);
      }

      return _Persons.Items;
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

    var updated = _Persons.AllItems.Append(newPerson).OrderBy(p => p, _PersonInfoComparer).ToArray();
    _Persons.Clear();
    _Persons.AddRange(updated);
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

      case string commandName when commandName == "ClearFilters":
        _Filter.Clear();
        break;

      case string commandName when commandName == "ToggleFilters":
        IsFiltersVisible = !IsFiltersVisible;
        break;

      case string commandName when commandName == "Refresh":
        this.RefreshView();
        break;
    }
  }
}
