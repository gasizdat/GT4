using GT4.Core.Project;

namespace GT4.UI;

using GT4.UI.Resources;

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
      using var token = new Core.Utils.DefaultCancellationToken();
      var ret = Services.GetRequiredService<IProjectList>()
        .GetItemsAsync(token)
        .Result
        .ToList();

      ret.Add(new ProjectItemCreate());

      return ret;
    }
  }

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    switch (e.CurrentSelection.FirstOrDefault())
    {
      case ProjectItemCreate item:
        await OnCreateProject();
        break;

      case ProjectItem projectItem:
        {
          using var token = new Core.Utils.DefaultCancellationToken();
          await Services.GetRequiredService<ICurrentProjectProvider>().OpenAsync(projectItem, token);
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
        UIStrings.AlertTextDeleteConfirmationText, UIStrings.BtnNameYes, UIStrings.BtnNameNo);

      if (result == false)
        return;

      using var token = new Core.Utils.DefaultCancellationToken();
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

      using var token = new Core.Utils.DefaultCancellationToken();
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