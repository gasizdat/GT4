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
  public ObservableCollection<ProjectListItem> Projects => new ObservableCollection<ProjectListItem>
        {
            new ProjectListItem { },
            new ProjectListItem { },
            new ProjectListItem { }
        };
}