using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Text;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectPage : ContentPage
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IComparer<Name> _NameComparer;
  private readonly IProjectList _ProjectList;
  private readonly IGedcomExporter _Exporter;

  private long? _ProjectRevision;

  public ProjectPage(IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _PersonInfoComparer = _ServiceProvider.GetKeyedService<IComparer<PersonInfo>>(PersonNamesFormat) ??
                          _ServiceProvider.GetRequiredService<IComparer<PersonInfo>>();
    _NameComparer = _ServiceProvider.GetRequiredService<IComparer<Name>>();
    _ProjectList = _ServiceProvider.GetRequiredService<IProjectList>();
    _Exporter = _ServiceProvider.GetRequiredService<IGedcomExporter>();

    PageCommand = new SafeCommand(OnPageCommand);
    InitializeComponent();
  }

  public ICollection<FamilyInfoItem> Families
  {
    get
    {
      try
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var ret = _CurrentProjectProvider
          .Project
          .FamilyManager
          .GetFamiliesAsync(token)
          .Result
          .Select(name => new FamilyInfoItem(name, GetFamilyPersons(name, token)))
          .OrderBy(item => item.Info, _NameComparer)
          .ToList();

        return ret;
      }
      catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
      {
        // The project was closed underneath us (e.g. the app is backgrounding). Nothing to surface.
        System.Diagnostics.Debug.WriteLine(ex);
        return [];
      }
      catch (Exception ex)
      {
        this.ShowErrorAsync(ex);
        return [];
      }
    }
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
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { ["FamilyName"] = item.Info });
      }
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      await this.ShowErrorAsync(ex);
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
        this.RefreshView();
      }
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      // Navigating in just as the project closes (e.g. backgrounding). Skip the revision refresh.
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private PersonInfo[] GetFamilyPersons(Name name, CancellationToken token)
  {
    var project = _CurrentProjectProvider.Project;
    _ProjectRevision = project.ProjectRevision;

    return project
      .PersonManager
      .GetPersonInfosByNameAsync(name: name, selectMainPhoto: true, token)
      .Result
      .OrderBy(item => item, _PersonInfoComparer)
      .ToArray();
  }

  private async Task OnPageCommand(object obj)
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
        this.RefreshView();
        break;

      case string commandName when commandName == "CreateFamily":
        await OnCreateFamily();
        break;

      case string commandName when commandName == "GoToNames":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<NamesPage>());
        break;

      case string commandName when commandName == "ExportGedcom":
        await OnExportGedcom();
        break;

      case string commandName when commandName == "GoToRevisions":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectRevisionsPage>());
        break;
    }
  }

  private async Task OnRemoveProject()
  {
    var projectName = _CurrentProjectProvider.Info.Name;
    var confirmationText = string.Format(UIStrings.AlertTextDeleteConfirmationText_1, projectName);
    if (await this.ShowConfirmationAsync(confirmationText))
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _ProjectList.RemoveAsync(projectName, token);
    }

    await Shell.Current.GoToAsync("..", true);
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

    await Shell.Current.GoToAsync("..", true);
  }

  private async Task OnCreateFamily()
  {
    var dialog = new CreateOrUpdateNameDialog(NameType.FamilyName, _ServiceProvider);

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

  private static string SanitizeFileName(string name)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    return string.IsNullOrWhiteSpace(sanitized) ? "project" : sanitized;
  }
}