using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectPage : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfoItem> _PersonInfoComparer;
  private readonly IComparer<FamilyInfoItem> _FamilyInfoComparer;
  private readonly INameFormatter _NameFormatter;
  private readonly IProjectList _ProjectList;

  private long? _ProjectRevision;

  private PersonInfoItem[] GetFamilyPersons(Name name, CancellationToken token)
  {
    var project = _CurrentProjectProvider.Project;
    _ProjectRevision = project.ProjectRevision;

    return project
      .PersonManager
      .GetPersonInfosByNameAsync(name: name, selectMainPhoto: true, token)
      .Result
      .Select(person => new PersonInfoItem(person, _NameFormatter))
      .OrderBy(item => item, _PersonInfoComparer)
      .ToArray();
  }

  private async void OnMenuItemCommand(object obj)
  {
    try
    {
      switch (obj)
      {
        case string name when name == "RemoveProject":
          await OnRemoveProject();
          break;

        case string name when name == "EditProject":
          await OnEditProject();
          break;
      }
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
  }

  private async Task OnRemoveProject()
  {
    var projectName = _CurrentProjectProvider.Info.Name;
    var confirmationText = string.Format(UIStrings.AlertTextDeleteConfirmationText_1, projectName);
    if (await PageAlert.ShowConfirmation(confirmationText))
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

    try
    {
      if (projectInfo.Name == string.Empty)
        return;

      var project = _CurrentProjectProvider.Project;
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();

      using var transaction = await project.BeginTransactionAsync(token);
      await Task.WhenAll(
        project.Metadata.SetProjectName(projectInfo.Name, token),
        project.Metadata.SetProjectDescription(projectInfo.Description, token));
      transaction.Commit();

      await Shell.Current.GoToAsync("..", true);
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
  }

  protected ProjectPage(IServiceProvider serviceProvider)
  {
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _PersonInfoComparer = serviceProvider.GetRequiredService<IComparer<PersonInfoItem>>();
    _FamilyInfoComparer = serviceProvider.GetRequiredService<IComparer<FamilyInfoItem>>();
    _NameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    _ProjectList = serviceProvider.GetRequiredService<IProjectList>();

    MenuItemCommand = new Command<object>(OnMenuItemCommand);
    InitializeComponent();
  }

  public ProjectPage()
    : this(ServiceBuilder.DefaultServices)
  {
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
          .OrderBy(item => item, _FamilyInfoComparer)
          .ToList();

        ret.Add(new FamilyInfoItemCreate());

        return ret;
      }
      catch (Exception ex)
      {
        return [new FamilyInfoItemRefresh(ex)];
      }
    }
  }

  public string RemoveProjectToolbarItemName =>
    string.Format(UIStrings.MenuItemNameRemove_1, _CurrentProjectProvider.Info.Name);

  public string EditProjectToolbarItemName =>
    string.Format(UIStrings.MenuItemNameEdit_1, _CurrentProjectProvider.Info.Name);


  public async void OnFamilySelected(object sender, SelectionChangedEventArgs e)
  {
    switch (e.CurrentSelection.FirstOrDefault())
    {
      case FamilyInfoItemCreate:
        await OnCreateFamily();
        break;
      case FamilyInfoItemRefresh:
        OnPropertyChanged(nameof(Families));
        break;
      case FamilyInfoItem item:
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { { "FamilyName", item.Info } });
        // TODO not so good approach
        if (sender is SelectableItemsView view)
        {
          view.SelectedItem = null;
        }
        break;
    }
  }

  public ICommand MenuItemCommand { get; init; }

  internal async Task OnCreateFamily()
  {
    var dialog = new CreateOrUpdateNameDialog(NameType.FamilyName);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
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
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
    finally
    {
      OnPropertyChanged(nameof(Families));
    }
  }

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);

    var projectRevision = _CurrentProjectProvider.Project.ProjectRevision;
    if (projectRevision != _ProjectRevision)
    {
      _ProjectRevision = projectRevision;
      OnPropertyChanged(nameof(Families));
    }
  }
}