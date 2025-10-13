using System.Windows.Input;

namespace GT4.UI;

public partial class MainPage : ContentPage
{

  public MainPage()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public ICommand NavigateToCreateOrOpenDialog => new Command(async () =>
    await Shell.Current.GoToAsync(UIRoutes.GetRoute<OpenOrCreateDialog>())
  );

  public string AppName => "Genealogy Tree 4";
  public string WelcomeTitle => "Create your own\ngenealogy tree and enjoy!";
  public string OpenOrCreateBtName => "Open or Create";
  public string OpenOrCreateBtHint => "Open an existing Genealogy Tree or create a new one.";
}
