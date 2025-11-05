
using GT4.Core.Project;
using GT4.Core.Utils;

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

  protected override Window CreateWindow(IActivationState? activationState)
  {
    var window = base.CreateWindow(activationState);
    window.Deactivated += SaveOnDeactivationAsync;
    window.Stopped += SaveOnDeactivationAsync;
    window.Destroying += SaveOnDisposeAsync;
    return window;
  }

  private async void SaveOnDeactivationAsync(object? sender, EventArgs e)
  {
    var token = ServiceBuilder.DefaultServices.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
    var projectProvider = ServiceBuilder.DefaultServices.GetRequiredService<ICurrentProjectProvider>();
    if (projectProvider.HasCurrentProject)
    {
      await projectProvider.UpdateOriginAsync(token);
    }
  }

  private async void SaveOnDisposeAsync(object? sender, EventArgs e)
  {
    var token = ServiceBuilder.DefaultServices.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
    var projectProvider = ServiceBuilder.DefaultServices.GetRequiredService<ICurrentProjectProvider>();
    if (projectProvider.HasCurrentProject)
    {
      await projectProvider.CloseAsync(token);
    }
  }
}
