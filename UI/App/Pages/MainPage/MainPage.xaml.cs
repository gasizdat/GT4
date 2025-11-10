using System.Collections;
using System.Globalization;
using System.Windows.Input;

namespace GT4.UI.App.Pages;

public partial class MainPage : ContentPage
{
  private static readonly Language _DefaultLanguage = new Language("en", "English");
  private static readonly List<Language> _Languages = new()
  {
    _DefaultLanguage,
    new Language("fr", "Français"),
    new Language("es", "Español"),
    new Language("de", "Deutsch"),
    new Language("it", "Italiano"),
    new Language("pt", "Português"),
    new Language("ru", "Русский"),
    new Language("zh", "中文"),
    new Language("ja", "日本語"),
    new Language("ko", "한국어")
  };

  private static Language _SelectedLanguage = GetCurrentLanguage();

  private static Language GetCurrentLanguage()
  {
    var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    var currentLanguage = _Languages.Where(lang => lang.Code == culture).FirstOrDefault() ?? _DefaultLanguage;
    return currentLanguage;
  }

  public MainPage()
  {
    InitializeComponent();
  }

  public ICommand NavigateToCreateOrOpenDialog => new Command(async () =>
    await Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectsPage>())
  );

  public IList Languages => _Languages;
  public Language SelectedLanguage
  {
    get => _SelectedLanguage;
    set
    {
      if (_SelectedLanguage == value)
        return;
      _SelectedLanguage = value;
      var culture = new CultureInfo(_SelectedLanguage.Code);
      Thread.CurrentThread.CurrentCulture = culture;
      Thread.CurrentThread.CurrentUICulture = culture;
      CultureInfo.DefaultThreadCurrentCulture = culture;
      CultureInfo.DefaultThreadCurrentUICulture = culture;
      var app = Application.Current as App;
      app?.MainPage?.Dispatcher.Dispatch(() => app.MainPage = new AppShell());
    }
  }
  public record class Language(string Code, string Name);
}
