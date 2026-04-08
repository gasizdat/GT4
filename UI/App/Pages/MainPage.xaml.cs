using System.Reflection;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class MainPage : ContentPage
{
  public MainPage()
  {
    InitializeComponent();
  }

  public ICommand NavigateToCreateOrOpenDialog => new SafeCommand(async () =>
    await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectListPage>())
  );

  public Utils.Language[] Languages => Utils.Language.Languages;

  public Utils.Language SelectedLanguage
  {
    get => Utils.Language.Current;
    set
    {
      if (Utils.Language.Current == value)
        return;

      Utils.Language.Current = value;
      var app = Application.Current as App;
      app?.MainPage?.Dispatcher.Dispatch(() => app.MainPage = new AppShell());
    }
  }

  public string AppVersion
  {
    get
    {
      var assembly = Assembly.GetExecutingAssembly();
      var version = assembly.GetName().Version;

      return $"({version?.ToString() ?? "??"})";
    }
  }
}
