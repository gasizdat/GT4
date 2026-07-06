using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectPage : ContentPage
{
  // GEDCOM has no standard MIME type: Windows filters on the ".ged" extension, while Android has none and
  // falls back to any file. Mirrors the picker on the project list's fresh-import command.
  private static readonly FilePickerFileType GedcomFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
  {
    [DevicePlatform.WinUI] = [".ged"],
    [DevicePlatform.Android] = ["*/*"],
  });

  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IComparer<Name> _NameComparer;
  private readonly IProjectList _ProjectList;
  private readonly IGedcomExporter _Exporter;
  private readonly IGedcomImporter _Importer;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;

  private static readonly BiologicalSex?[] SexFilterValues = [null, BiologicalSex.Male, BiologicalSex.Female, BiologicalSex.Unknown];
  private static readonly bool?[] MaritalStatusFilterValues = [null, true, false];

  private long? _ProjectRevision;
  private readonly FilteredObservableCollection<FamilyInfoItem> _Families = new();
  private bool _FamiliesLoaded;
  private Dictionary<int, bool> _IsMarried = new();
  private double _MinYear;
  private double _MaxYear;
  private string _NameFilter = string.Empty;
  private int _SexFilterIndex;
  private int _MaritalStatusFilterIndex;
  private bool _IsYearFilterEnabled;
  private double _SelectedYear;
  private bool _IsFiltersVisible;

  public ProjectPage(
    INameTypeFormatter nameTypeFormatter,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    [FromKeyedServices(NameFormat.ShortPersonName)]
    IComparer<PersonInfo>? personInfoComparerByShortNames,
    IComparer<PersonInfo> personInfoComparer,
    IComparer<Name> nameComparer,
    IProjectList projectList,
    IGedcomExporter exporter,
    IGedcomImporter importer,
    IAlertService alertService,
    INavigationService navigationService,
    IBiologicalSexFormatter biologicalSexFormatter
    )
  {
    _NameTypeFormatter = nameTypeFormatter;
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _PersonInfoComparer = personInfoComparerByShortNames ?? personInfoComparer;
    _NameComparer = nameComparer;
    _ProjectList = projectList;
    _Exporter = exporter;
    _Importer = importer;
    _AlertService = alertService;
    _NavigationService = navigationService;

    SexFilterLabels =
    [
      UIStrings.FieldFilterAny,
      biologicalSexFormatter.ToString(BiologicalSex.Male),
      biologicalSexFormatter.ToString(BiologicalSex.Female),
      biologicalSexFormatter.ToString(BiologicalSex.Unknown),
    ];
    MaritalStatusFilterLabels =
    [
      UIStrings.FieldFilterAny,
      UIStrings.FieldMaritalStatusMarried,
      UIStrings.FieldMaritalStatusSingle,
    ];

    // Set once: family visibility is re-evaluated via _Families.Update() (through UpdateFamilies),
    // not by reassigning this predicate.
    _Families.Filter = (_, family) => family.HasVisiblePersons;

    PageCommand = new SafeCommand(OnPageCommand, _AlertService);
    InitializeComponent();
  }

  public ObservableCollection<FamilyInfoItem> Families
  {
    get
    {
      EnsureFamiliesLoaded();
      return _Families.Items;
    }
  }

  private void EnsureFamiliesLoaded()
  {
    if (_FamiliesLoaded)
    {
      return;
    }

    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      _ProjectRevision = project.ProjectRevision;

      var personsByFamilyNameId = project
        .PersonManager
        .GetPersonInfosAsync(selectMainPhoto: true, token)
        .Result
        .SelectMany(person => person.Names.Select(name => (NameId: name.Id, Person: person)))
        .ToLookup(x => x.NameId, x => x.Person);

      var familyPersons = project
        .FamilyManager
        .GetFamiliesAsync(token)
        .Result
        .Select(name => (Family: name, Persons: personsByFamilyNameId[name.Id].OrderBy(item => item, _PersonInfoComparer).ToArray()))
        .ToList();

      var allPersons = familyPersons.SelectMany(f => f.Persons).DistinctBy(p => p.Id).ToArray();
      var relatives = project.Relatives.GetRelativesForPersonsAsync(allPersons, token).Result;
      _IsMarried = relatives.ToDictionary(kv => kv.Key, kv => kv.Value.Any(r => r.Type == RelationshipType.Spouse));

      (_MinYear, _MaxYear) = ComputeYearBounds(allPersons);
      if (_SelectedYear < _MinYear || _SelectedYear > _MaxYear)
      {
        _SelectedYear = _MaxYear;
      }

      var families = familyPersons
        .Select(f => new FamilyInfoItem(f.Family, f.Persons, PersonMatches))
        .OrderBy(item => item.Info, _NameComparer)
        .ToList();

      _Families.Clear();
      _Families.AddRange(families);
      _FamiliesLoaded = true;
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      // The project was closed underneath us (e.g. the app is backgrounding). Nothing to surface.
      System.Diagnostics.Debug.WriteLine(ex);
      _Families.Clear();
      _FamiliesLoaded = true;
    }
    catch (Exception ex)
    {
      _ = _AlertService.ShowErrorAsync(ex);
      _Families.Clear();
      _FamiliesLoaded = true;
    }
  }

  // Loops AllItems, not the currently-visible Items: a family hidden by the current filters must
  // still get its inner filter refreshed in case it becomes visible again.
  private void UpdateFamilies()
  {
    foreach (var family in _Families.AllItems)
    {
      family.Update();
    }

    _Families.Update();
  }

  private static (double Min, double Max) ComputeYearBounds(IReadOnlyCollection<PersonInfo> persons)
  {
    var knownYears = persons
      .SelectMany(p => new[]
      {
        p.BirthDate.Status == DateStatus.Unknown ? (int?)null : p.BirthDate.Year,
        p.DeathDate is { Status: not DateStatus.Unknown } d ? d.Year : (int?)null,
      })
      .Where(y => y.HasValue)
      .Select(y => y!.Value)
      .ToList();

    var currentYear = Date.Now.Year;
    var min = knownYears.Count > 0 ? knownYears.Min() : currentYear - 100;
    var max = Math.Max(knownYears.Count > 0 ? knownYears.Max() : currentYear, currentYear);

    return (min, max);
  }

  private bool PersonMatches(FilteredObservableCollection<PersonInfo> _, PersonInfo person)
  {
    if (!person.Names.Any(n => WildcardMatcher.IsMatch(n.Value, _NameFilter)))
    {
      return false;
    }

    if (CurrentSex is { } sex && person.BiologicalSex != sex)
    {
      return false;
    }

    if (CurrentMaritalStatus is { } wantMarried)
    {
      var isMarried = _IsMarried.TryGetValue(person.Id, out var married) && married;
      if (wantMarried != isMarried)
      {
        return false;
      }
    }

    if (_IsYearFilterEnabled && !PersonLifetimeMatcher.IsAliveInYear(person, (int)_SelectedYear))
    {
      return false;
    }

    return true;
  }

  public string[] SexFilterLabels { get; }

  public string[] MaritalStatusFilterLabels { get; }

  private BiologicalSex? CurrentSex => SexFilterValues[_SexFilterIndex];

  private bool? CurrentMaritalStatus => MaritalStatusFilterValues[_MaritalStatusFilterIndex];

  public string NameFilter
  {
    get => _NameFilter;
    set
    {
      _NameFilter = value;
      OnPropertyChanged(nameof(NameFilter));
      UpdateFamilies();
    }
  }

  public int SexFilterIndex
  {
    get => _SexFilterIndex;
    set
    {
      _SexFilterIndex = value;
      OnPropertyChanged(nameof(SexFilterIndex));
      UpdateFamilies();
    }
  }

  public int MaritalStatusFilterIndex
  {
    get => _MaritalStatusFilterIndex;
    set
    {
      _MaritalStatusFilterIndex = value;
      OnPropertyChanged(nameof(MaritalStatusFilterIndex));
      UpdateFamilies();
    }
  }

  public bool IsYearFilterEnabled
  {
    get => _IsYearFilterEnabled;
    set
    {
      _IsYearFilterEnabled = value;
      OnPropertyChanged(nameof(IsYearFilterEnabled));
      UpdateFamilies();
    }
  }

  public double MinYear
  {
    get
    {
      EnsureFamiliesLoaded();
      return _MinYear;
    }
  }

  public double MaxYear
  {
    get
    {
      EnsureFamiliesLoaded();
      return _MaxYear;
    }
  }

  public double SelectedYear
  {
    get => _SelectedYear;
    set
    {
      _SelectedYear = value;
      OnPropertyChanged(nameof(SelectedYear));
      UpdateFamilies();
    }
  }

  public bool IsFiltersVisible
  {
    get => _IsFiltersVisible;
    set
    {
      _IsFiltersVisible = value;
      OnPropertyChanged(nameof(IsFiltersVisible));
      OnPropertyChanged(nameof(ToggleFiltersButtonName));
    }
  }

  public string ToggleFiltersButtonName =>
    string.Format(UIStrings.BtnNameFilters_1, IsFiltersVisible ? "🔼" : "🔽");

  private void OnClearFilters()
  {
    _NameFilter = string.Empty;
    _SexFilterIndex = 0;
    _MaritalStatusFilterIndex = 0;
    _IsYearFilterEnabled = false;
    _SelectedYear = _MaxYear;

    OnPropertyChanged(nameof(NameFilter));
    OnPropertyChanged(nameof(SexFilterIndex));
    OnPropertyChanged(nameof(MaritalStatusFilterIndex));
    OnPropertyChanged(nameof(IsYearFilterEnabled));
    OnPropertyChanged(nameof(SelectedYear));
    UpdateFamilies();
  }

  public string RemoveProjectToolbarItemName =>
    string.Format(UIStrings.MenuItemNameRemove_1, _CurrentProjectProvider.Info.Name);

  public string EditProjectToolbarItemName =>
    string.Format(UIStrings.MenuItemNameEdit_1, _CurrentProjectProvider.Info.Name);

  public async void OnFamilySelected(object sender, SelectionChangedEventArgs e)
  {
    // async void event handler: an escaped exception is unobserved and crashes the app, so guard it.
    try
    {
      if (e.CurrentSelection.FirstOrDefault() is FamilyInfoItem item)
      {
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { ["FamilyName"] = item.Info });
      }
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      await _AlertService.ShowErrorAsync(ex);
    }
  }

  public ICommand PageCommand { get; init; }

  public NameFormat PersonNamesFormat => NameFormat.ShortPersonName;

  // Guards against the re-entrancy caused by writing WidthRequest below, which itself
  // triggers another SizeChanged on the same FlexLayout.
  private bool _EqualizingPersonWidths;

  // Makes every person item in a family card share the width of the widest item.
  // FlexLayout has no "size all children to the widest" mode, so we measure each
  // item's natural width and write the max back as a WidthRequest.
  private void OnFamilyPersonsSizeChanged(object? sender, EventArgs e)
  {
    if (sender is not FlexLayout flex || _EqualizingPersonWidths)
    {
      return;
    }

    _EqualizingPersonWidths = true;
    try
    {
      var maxWidth = 0d;
      foreach (var child in flex.Children)
      {
        if (child is not IView view)
        {
          continue;
        }

        // Clear any width from a previous pass so we measure the item's natural width.
        // This matters when the global font scale changes the content size in place.
        if (child is VisualElement element)
        {
          element.WidthRequest = -1;
        }

        var desired = view.Measure(double.PositiveInfinity, double.PositiveInfinity);
        maxWidth = Math.Max(maxWidth, desired.Width);
      }

      if (maxWidth <= 0)
      {
        return;
      }

      foreach (var child in flex.Children)
      {
        if (child is VisualElement element)
        {
          element.WidthRequest = maxWidth;
        }
      }
    }
    finally
    {
      _EqualizingPersonWidths = false;
    }
  }

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);

    try
    {
      var projectRevision = _CurrentProjectProvider.Project.ProjectRevision;
      if (projectRevision != _ProjectRevision)
      {
        _ProjectRevision = projectRevision;
        _FamiliesLoaded = false;
        this.RefreshView();
      }
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      // Navigating in just as the project closes (e.g. backgrounding). Skip the revision refresh.
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected async Task OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "RemoveProject":
        await OnRemoveProject();
        break;

      case string commandName when commandName == "EditProject":
        await OnEditProject();
        break;

      case string commandName when commandName == "Refresh":
        _FamiliesLoaded = false;
        this.RefreshView();
        break;

      case string commandName when commandName == "ClearFilters":
        OnClearFilters();
        break;

      case string commandName when commandName == "ToggleFilters":
        IsFiltersVisible = !IsFiltersVisible;
        break;

      case string commandName when commandName == "CreateFamily":
        await OnCreateFamily();
        break;

      case string commandName when commandName == "GoToNames":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<NamesPage>());
        break;

      case string commandName when commandName == "ExportGedcom":
        await OnExportGedcom();
        break;

      case string commandName when commandName == "ImportGedcom":
        await OnImportGedcom();
        break;

      case string commandName when commandName == "GoToRevisions":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<ProjectRevisionsPage>());
        break;
    }
  }

  private async Task OnRemoveProject()
  {
    var projectName = _CurrentProjectProvider.Info.Name;
    var projectOrigin = _CurrentProjectProvider.Info.Origin;
    var confirmationText = string.Format(UIStrings.AlertTextDeleteConfirmationText_1, projectName);
    if (await _AlertService.ShowConfirmationAsync(confirmationText))
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _ProjectList.RemoveAsync(projectOrigin, token);
    }

    await _NavigationService.GoToAsync("..", true);
  }

  private async Task OnEditProject()
  {
    var dialog = new CreateOrUpdateProjectDialog(_CurrentProjectProvider.Info);

    await Navigation.PushModalAsync(dialog);
    var projectInfo = await dialog.ProjectInfo;
    await Navigation.PopModalAsync();

    if (projectInfo.Name == string.Empty)
      return;

    var project = _CurrentProjectProvider.Project;
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();

    using var transaction = await project.BeginTransactionAsync(token);
    await Task.WhenAll(
      project.Metadata.SetProjectNameAsync(projectInfo.Name, token),
      project.Metadata.SetProjectDescriptionAsync(projectInfo.Description, token));
    await transaction.CommitAsync(token);

    await _NavigationService.GoToAsync("..", true);
  }

  private async Task OnCreateFamily()
  {
    var dialog = new CreateOrUpdateNameDialog(NameType.FamilyName, _NameTypeFormatter);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var family = await _CurrentProjectProvider
      .Project
      .FamilyManager
      .AddFamilyAsync(familyName: info.Name, maleLastName: info.MaleName, femaleLastName: info.FemaleName, token);
  }

  // Exports the open project to a GEDCOM file in the cache directory and hands it to the OS share sheet,
  // which lets the user save or send it. GEDCOM 5.5.1 is UTF-8, written without a BOM.
  private async Task OnExportGedcom()
  {
    var fileName = SanitizeFileName(_CurrentProjectProvider.Info.Name) + ".ged";
    var path = Path.Combine(FileSystem.CacheDirectory, fileName);

    await using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _Exporter.ExportAsync(_CurrentProjectProvider.Project, writer, token);
    }

    var request = new ShareFileRequest { Title = UIStrings.ShareGedcomTitle, File = new ShareFile(path) };
    await Share.Default.RequestAsync(request);
  }

  // Merges a GEDCOM file into the open project. Unlike the project list's import (which always lands in a
  // fresh project), this folds people that match an existing person and adds the rest. It mutates the
  // current project, so it is confirmed first; the import is one transaction, so a cancellation or a
  // malformed file rolls back and leaves the project untouched. The slow work runs on a background thread
  // behind the cancellable import modal.
  private async Task OnImportGedcom()
  {
    var pickOptions = new PickOptions { PickerTitle = UIStrings.FileDialogSelectGedcom, FileTypes = GedcomFileType };
    var file = await FilePicker.Default.PickAsync(pickOptions);
    if (file is null)
      return;

    var confirmText = string.Format(UIStrings.AlertImportGedcomConfirm_1, _CurrentProjectProvider.Info.Name);
    if (!await _AlertService.ShowConfirmationAsync(confirmText))
      return;

    var dialog = new GedcomImportDialog(_CurrentProjectProvider.Info.Name);
    await Navigation.PushModalAsync(dialog);
    try
    {
      await Task.Run(() => RunImportAsync(file, dialog.Token));
    }
    catch (OperationCanceledException)
    {
      // Cancelled mid-import: the single import transaction rolls back, so the project is unchanged.
    }
    finally
    {
      await Navigation.PopModalAsync();
    }

    _FamiliesLoaded = false;
    this.RefreshView();
  }

  private async Task RunImportAsync(FileResult file, CancellationToken token)
  {
    using var stream = await file.OpenReadAsync();
    using var reader = new StreamReader(stream, Encoding.UTF8);
    var mediaBasePath = MediaBasePath(file);
    await _Importer.ImportAsync(_CurrentProjectProvider.Project, reader, token, mediaBasePath);
  }

  // External OBJE FILE images are resolved relative to the picked .ged file's folder. On desktop FullPath is
  // the real path; where it is unavailable (e.g. a mobile content URI) the importer simply finds no siblings
  // and leaves those references as residue.
  private static string? MediaBasePath(FileResult file) =>
    string.IsNullOrEmpty(file.FullPath) ? null : Path.GetDirectoryName(file.FullPath);

  private static string SanitizeFileName(string name)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    return string.IsNullOrWhiteSpace(sanitized) ? "project" : sanitized;
  }
}