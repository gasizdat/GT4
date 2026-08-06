using GT4.Core.Gedcom;
using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectPage : ContentPage
{
  // GEDCOM has no standard MIME type: Windows filters on the ".ged" extension (and ".zip", which is what an
  // export produces), while Android has none and falls back to any file. Mirrors the picker on the project
  // list's fresh-import command.
  private static readonly FilePickerFileType GedcomFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
  {
    [DevicePlatform.WinUI] = [".ged", ".zip"],
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
  private readonly GedcomImportEncoding _GedcomImportEncoding;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;

  private readonly FilteredObservableCollection<FamilyInfoItem> _Families = new();
  private bool _FamiliesLoaded;
  private ProjectInfo? _LastProjectInfo;

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
    GedcomImportEncoding gedcomImportEncoding,
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
    _GedcomImportEncoding = gedcomImportEncoding;
    _AlertService = alertService;
    _NavigationService = navigationService;

    // Set once: family visibility is re-evaluated via _Families.Update() (through UpdateFamilies),
    // not by reassigning this predicate.
    _Families.Filter = (_, family) => family.HasVisiblePersons;

    PageCommand = new SafeCommand(OnPageCommand, _AlertService);
    InitializeComponent();

    FilterView.Initialize(
      biologicalSexFormatter,
      _CancellationTokenProvider,
      _CurrentProjectProvider,
      _AlertService,
      () => [.. _Families.AllItems.SelectMany(f => f.AllPersons).DistinctBy(p => p.Id)]);
    FilterView.Changed += (_, _) => UpdateFamilies();
    _LastProjectInfo = _CurrentProjectProvider.Info;
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
    _FamiliesLoaded = true;

    async Task OnLoadFamiliesAsync()
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      var startInfo = _CurrentProjectProvider.Info;

      var persons = await project
          .PersonManager
          .GetPersonInfosWithPhotoMetadataAsync(token);
      var familyNames = await project
          .FamilyManager
          .GetFamiliesAsync(token);
      var personsByFamilyNameId = persons
        .SelectMany(person => person.Names.Select(name => (NameId: name.Id, Person: person)))
        .ToLookup(x => x.NameId, x => x.Person);

      var familyPersons = familyNames
        .Select(name => (Family: name, Persons: personsByFamilyNameId[name.Id].OrderBy(item => item, _PersonInfoComparer)));

      var families = familyPersons
        .Select(f => new FamilyInfoItem(f.Family, [.. f.Persons], (_, person) => FilterView.Matches(person)))
        .OrderBy(item => item.Info, _NameComparer)
        .ToList();

      var familylessPersons = persons
        .Where(FamilyInfoItem.HasNoFamily)
        .OrderBy(item => item, _PersonInfoComparer)
        .ToArray();
      if (familylessPersons.Length > 0)
      {
        families.Add(new FamilyInfoItem(FamilyInfoItem.NoFamilyName, familylessPersons, (_, person) => FilterView.Matches(person)));
      }

      // Clear and AddRange together, not eagerly when the load starts: an overlapping second load
      // (Refresh() can re-enter this while one is in flight) would otherwise append onto a stale,
      // already-cleared collection and duplicate every card. Compare startInfo (this load's start
      // revision), not the _LastProjectInfo field a concurrent Refresh() may have already advanced.
      await SafeTask.RunOnMainThread(() =>
      {
        if (startInfo != _CurrentProjectProvider.Info)
        {
          Refresh();
          return;
        }

        _Families.Clear();
        _Families.AddRange(families);
      }, _AlertService);
    }

    SafeTask.Run(OnLoadFamiliesAsync, _AlertService);
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

  public string RemoveProjectToolbarItemName =>
    string.Format(UIStrings.MenuItemNameRemove_1, _CurrentProjectProvider.Info.Name);

  public string EditProjectToolbarItemName =>
    string.Format(UIStrings.MenuItemNameEdit_1, _CurrentProjectProvider.Info.Name);

  public async void OnFamilySelected(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.FirstOrDefault() is FamilyInfoItem item)
    {
      async Task GoToFamilyAsync()
      {
        var route = UIRoutes.GetRoute<FamilyPage>();
        await _NavigationService.GoToAsync(route, true, new() { ["FamilyName"] = item.Info });
      }

      await SafeTask.GuardAsync(GoToFamilyAsync, _AlertService);
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

  private void Refresh()
  {
    _LastProjectInfo = _CurrentProjectProvider.Info;
    _FamiliesLoaded = false;
    this.RefreshView();
  }

  // Catches a change committed on a subpage: returning here re-navigates, so compare the live project
  // revision (carried on Info) against the snapshot taken when this page last loaded its data.
  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    if (_LastProjectInfo != _CurrentProjectProvider.Info)
    {
      Refresh();
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
        Refresh();
        break;

      case string commandName when commandName == "CreateFamily":
        await OnCreateFamily();
        break;

      case string commandName when commandName == "GoToNames":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<NamesPage>());
        break;

      case string commandName when commandName == "GoToStatistics":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<StatisticsPage>());
        break;

      case string commandName when commandName == "GoToKinshipFinder":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<KinshipFinderPage>());
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
    var dialog = new CreateOrUpdateProjectDialog(_CurrentProjectProvider.Info, _AlertService);

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
    var dialog = new CreateOrUpdateNameDialog(NameType.FamilyName, _NameTypeFormatter, _AlertService);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider
      .Project
      .FamilyManager
      .AddFamilyAsync(familyName: info.Name, maleLastName: info.MaleName, femaleLastName: info.FemaleName, token);

    Refresh();
  }

  // Exports the open project to a GEDCOM package in the cache directory and hands it to the OS share sheet,
  // which lets the user save or send it. The media the document references travels beside the .ged rather
  // than inside it, so the two are shared as one archive.
  private async Task OnExportGedcom()
  {
    var name = FileNameUtils.Sanitize(_CurrentProjectProvider.Info.Name, "project");
    var path = Path.Combine(FileSystem.CacheDirectory, name + GedcomPackage.ArchiveExtension);

    await using (var archive = new FileStream(path, FileMode.Create))
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await GedcomPackage.WriteAsync(_Exporter, _CurrentProjectProvider.Project, archive, name + ".ged", token);
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

    using var source = await GedcomImportSource.OpenAsync(file, FileSystem.CacheDirectory);
    using var reader = await _GedcomImportEncoding.ResolveReaderAsync(source.OpenStreamAsync, Navigation);
    if (reader is null)
      return;

    var dialog = new GedcomImportDialog(_CurrentProjectProvider.Info.Name, _AlertService);
    await Navigation.PushModalAsync(dialog);
    try
    {
      await Task.Run(() => RunImportAsync(source, reader, dialog.Token));
    }
    catch (OperationCanceledException)
    {
      // Cancelled mid-import: the single import transaction rolls back, so the project is unchanged.
    }
    finally
    {
      await Navigation.PopModalAsync();
    }

    Refresh();
  }

  private async Task RunImportAsync(GedcomImportSource source, TextReader reader, CancellationToken token) =>
    await _Importer.ImportAsync(_CurrentProjectProvider.Project, reader, token, source.MediaBasePath);
}
