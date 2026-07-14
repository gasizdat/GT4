using GT4.UI.Abstraction;
using GT4.UI.Utils.Settings;
using System.Reflection;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class MainPage : ContentPage
{
  private readonly LanguageSetting _LanguageSetting;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;

  public MainPage(LanguageSetting languageSetting, IAlertService alertService, INavigationService navigationService)
  {
    _LanguageSetting = languageSetting;
    _AlertService = alertService;
    _NavigationService = navigationService;
    InitializeComponent();
  }

  public ICommand PageCommand => new SafeCommand(async (object arg) =>
  {
    switch (arg)
    {
      case string commandName when commandName == "OpenOrCreateDialog":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<ProjectListPage>());
        break;
      case string commandName when commandName == "OpenSettings":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<SettingsPage>());
        break;
    }
  }, _AlertService);

  public Utils.Language[] Languages => Utils.Language.Languages;

  public Utils.Language SelectedLanguage
  {
    get => Utils.Language.Current;
    set
    {
      if (Utils.Language.Current == value)
        return;

      Utils.Language.Current = value;
      _LanguageSetting.Value = value;

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
