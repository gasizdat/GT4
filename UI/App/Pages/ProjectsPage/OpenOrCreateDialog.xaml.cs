using GT4.Core.Project;
using GT4.Core.Utils;
using GT4.UI.App.Dialogs;
using GT4.UI.App.Items;
using GT4.UI.Resources;

namespace GT4.UI.App.Pages;

public partial class OpenOrCreateDialog : ContentPage
{
  public OpenOrCreateDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public ServiceProvider Services { get; set; } = ServiceBuilder.DefaultServices;

  public ICollection<ProjectItem> Projects
  {
    get
    {
      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      var ret = Services.GetRequiredService<IProjectList>()
        .GetItemsAsync(token)
        .Result
        .Select(projectInfo => new ProjectItem(projectInfo))
        .ToList();

      ret.Sort(Services.GetRequiredService<IComparer<ProjectItem>>());

      ret.Add(new ProjectItemCreate());

      return ret;
    }
  }

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    switch (e.CurrentSelection.FirstOrDefault())
    {
      case ProjectItemCreate:
        await OnCreateProject();
        break;

      case ProjectItem projectItem:
        {
          using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
          await Services.GetRequiredService<ICurrentProjectProvider>().OpenAsync(projectItem.Info, token);
          await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamiliesPage>());

          // TODO not so good approach
          if (sender is SelectableItemsView view)
          {
            view.SelectedItem = null;
          }
          break;
        }
    }
  }

  public async void OnDeleteProjectSelected(object sender, EventArgs e)
  {
    var item = (sender as BindableObject)?.BindingContext as ProjectItem;
    if (item is null or ProjectItemCreate)
      return;

    try
    {
      var result = await DisplayAlert(UIStrings.AlertTitleConfirmation,
        string.Format(UIStrings.AlertTextDeleteConfirmationText_1, item.Name), UIStrings.BtnNameYes, UIStrings.BtnNameNo);

      if (result == false)
        return;

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      await Services.GetRequiredService<IProjectList>().RemoveAsync(item.Name, token);
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
    finally
    {
      OnPropertyChanged(nameof(Projects));
    }
  }

  internal async Task OnCreateProject()
  {
    var dialog = new CreateNewProjectDialog();

    await Navigation.PushModalAsync(dialog);
    var projectInfo = await dialog.ProjectInfo;
    await Navigation.PopModalAsync();

    try
    {
      if (projectInfo.Name == string.Empty)
        return;

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      await Services.GetRequiredService<IProjectList>().CreateAsync(projectInfo, token);
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
    finally
    {
      OnPropertyChanged(nameof(Projects));
    }
  }
}