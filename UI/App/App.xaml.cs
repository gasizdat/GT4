namespace GT4.UI.App;

public partial class App : Application
{
  public App()
  {
    InitializeComponent();

#if ANDROID
    Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.UseWindowSoftInputModeAdjust(
      Current?.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>(),
      Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif

    MainPage = new AppShell();
  }
}
