using System.Reflection;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class MainPage : ContentPage
{
  public MainPage()
  {
    InitializeComponent();
  }

  public ICommand PageCommand => new SafeCommand(async (object arg) =>
  {
    switch (arg)
    {
      case string commandName when commandName == "OpenOrCreateDialog":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectListPage>());
        break;
      case string commandName when commandName == "OpenSettings":
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<SettingsPage>());
        break;
    }
  });

  public Utils.Language[] Languages => Utils.Language.Languages;

  public Utils.Language SelectedLanguage
  {
    get => Utils.Language.Current;
    set
    {
      if (Utils.Language.Current == value)
        return;

      Utils.Language.Current = value;

      var mainWindow = Application.Current?.Windows.SingleOrDefault(w => w.Page is AppShell);
      if (mainWindow is not null)
      {
        mainWindow.Dispatcher.Dispatch(() => mainWindow.Page = new AppShell());
      }
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
