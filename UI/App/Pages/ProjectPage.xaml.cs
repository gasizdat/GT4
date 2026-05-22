using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
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
    if (e.CurrentSelection.FirstOrDefault() is FamilyInfoItem item)
    {
      await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { ["FamilyName"] = item.Info });
    }
  }

  public ICommand PageCommand { get; init; }

  public NameFormat PersonNamesFormat => NameFormat.ShortPersonName;

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);

    var projectRevision = _CurrentProjectProvider.Project.ProjectRevision;
    if (projectRevision != _ProjectRevision)
    {
      _ProjectRevision = projectRevision;
      this.RefreshView();
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
    transaction.Commit();

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
}