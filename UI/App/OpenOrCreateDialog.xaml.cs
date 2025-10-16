using GT4.Project;
using System.Collections.ObjectModel;

namespace GT4.UI;

public partial class OpenOrCreateDialog : ContentPage
{
  private readonly ServiceProvider _services = ServiceBuilder.DefaultServices;

  public OpenOrCreateDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public string DialogTitle => "Open or Create a Genealogy";
  public string DialogHint => "You can open the existent or create a new Genealogy Tree";
  public ICollection<ProjectListItem> Projects
  {
    get
    {
      var ret = new Collection<ProjectListItem> { };

      _services.GetRequiredService<IProjectList>()
        .Items
        .Result
        .ToList()
        .ForEach(item => ret.Add(new ProjectListItem(item)));

      ret.Add(new ProjectListItemCreate());

      return ret;
    }
  }

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.FirstOrDefault() is ProjectListItemCreate item)
    {
      await OnCreateProject();
    }
  }

  public async void OnDeleteProjectSelected(object sender, EventArgs e)
  {

    var item = (sender as BindableObject)?.BindingContext as ProjectListItem;
    if (item is null or ProjectListItemCreate)
      return;

    try
    {
      var result = await DisplayAlert("Info", $"Are you really want to delete {item.Name}", "Yes", "No");
      if (result == false)
        return;

      await _services.GetRequiredService<IProjectList>().RemoveAsync(item.Name);
    }
    catch (Exception ex)
    {
      await DisplayAlert("Error", ex.Message, "OK");
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

      await _services.GetRequiredService<IProjectList>().CreateAsync(projectInfo);
    }
    catch (Exception ex)
    {
      await DisplayAlert("Error", ex.Message, "OK");
    }
    finally
    {
      OnPropertyChanged(nameof(Projects));
    }
  }
}