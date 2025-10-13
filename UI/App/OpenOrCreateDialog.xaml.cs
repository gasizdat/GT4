using System.Collections.ObjectModel;

namespace GT4.UI;

public partial class OpenOrCreateDialog : ContentPage
{
  public OpenOrCreateDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public string DialogTitle => "Open or Create a Genealogy";
  public string DialogHint => "You can open the existent or create a new Genealogy Tree";
  public ObservableCollection<ProjectListItem> Projects
  {
    get
    {
      var ret = new ObservableCollection<ProjectListItem>
      {
        new ProjectListItemCreate{ }
      };

      using var projectList = Utils.ServiceBuilder.DefaultServices.GetService<Utils.IProjectList>() 
        ?? throw new ApplicationException("Cannot get IProjectList service");
      projectList
        .Items
        .ToList()
        .ForEach(i => ret.Add(new ProjectListItem { Name = i.Name, Path = i.Path }));

      return ret;
    }
  }

  public async void OnProjectSelected(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.FirstOrDefault() is ProjectListItemCreate item)
    {
      await Shell.Current.GoToAsync(UIRoutes.GetRoute<CreateNewProjectDialog>());
    }
    //App.Current.MainPage = new MainPage(item.Name, item.Path);
  }
}