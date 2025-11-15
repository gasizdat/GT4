using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class FamiliesPage : ContentPage
{
  private long? _ProjectRevision;

  private PersonInfoItem[] GetFamilyPersons(Name name, CancellationToken token)
  {
    var project = Services.GetRequiredService<ICurrentProjectProvider>().Project;
    var nameFormatter = Services.GetRequiredService<INameFormatter>();
    _ProjectRevision = project.ProjectRevision;

    return project
      .PersonManager
      .GetPersonInfosByNameAsync(name, token)
      .Result
      .Select(person => new PersonInfoItem(person, nameFormatter))
      .OrderBy(item => item, Services.GetRequiredService<IComparer<PersonInfoItem>>())
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
    var projectName = Services.GetRequiredService<ICurrentProjectProvider>().Info.Name;
    var confirmationText = string.Format(UIStrings.AlertTextDeleteConfirmationText_1, projectName);
    if (await PageAlert.ShowConfirmation(confirmationText))
    {
      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      await Services.GetRequiredService<IProjectList>().RemoveAsync(projectName, token);
    }

    await Shell.Current.GoToAsync("..", true);
  }

  public FamiliesPage()
  {
    MenuItemCommand = new Command<object>(OnMenuItemCommand);
    InitializeComponent();
  }

  public ServiceProvider Services { get; set; } = ServiceBuilder.DefaultServices;

  public ICollection<FamilyInfoItem> Families
  {
    get
    {
      try
      {
        using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
        var ret = Services.GetRequiredService<ICurrentProjectProvider>()
          .Project
          .FamilyManager
          .GetFamiliesAsync(token)
          .Result
          .Select(name => new FamilyInfoItem(name, GetFamilyPersons(name, token)))
          .OrderBy(item => item, Services.GetRequiredService<IComparer<FamilyInfoItem>>())
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
    string.Format(UIStrings.MenuItemNameRemove_1, Services.GetRequiredService<ICurrentProjectProvider>().Info.Name);

  public string EditProjectToolbarItemName =>
    string.Format(UIStrings.MenuItemNameEdit_1, Services.GetRequiredService<ICurrentProjectProvider>().Info.Name);


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
    var dialog = new CreateNewNameDialog(NameType.FamilyName);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
      if (info is null)
      {
        return;
      }

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      var family = await Services.GetRequiredService<ICurrentProjectProvider>()
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

    var projectRevision = Services.GetRequiredService<ICurrentProjectProvider>().Project.ProjectRevision;
    if (projectRevision != _ProjectRevision)
    {
      _ProjectRevision = projectRevision;
      OnPropertyChanged(nameof(Families));
    }
  }
}