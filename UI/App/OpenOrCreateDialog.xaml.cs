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
        new ProjectListItem { Name = "Create New!", Path = "Create new Genealogy Tree" }
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
}