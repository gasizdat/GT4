namespace GT4.UI;

public partial class CreateNewProjectDialog : ContentPage
{
  public CreateNewProjectDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public string CreateProjectBtName => "Create Genealogy Tree";
  public string DialogTitle => "Create a Genealogy Tree";
  public string ProjectNamePlaceholder => "Enter a name for the new Genealogy Tree";
  public string ProjectName { get; set; } = string.Empty;
}