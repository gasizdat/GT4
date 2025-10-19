using GT4.Core.Project;
using System.Collections.ObjectModel;

namespace GT4.UI;

using GT4.UI.Resources;

public partial class FamiliesPage : ContentPage
{
  private readonly ServiceProvider _services = ServiceBuilder.DefaultServices;

  public FamiliesPage()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public ICollection<ProjectItem> Projects
  {
    get
    {
      using var token = new Core.Utils.DefaultCancellationToken();
      var ret = _services.GetRequiredService<IProjectList>()
        .GetItemsAsync(token)
        .Result
        .ToList();

      ret.Add(new ProjectItemCreate());

      return ret;
    }
  }

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.FirstOrDefault() is ProjectItemCreate item)
    {
      await OnCreateProject();
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
      await _services.GetRequiredService<IProjectList>().RemoveAsync(item.Name, token);
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
      await _services.GetRequiredService<IProjectList>().CreateAsync(projectInfo, token);
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