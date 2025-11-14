
using GT4.Core.Project;
using GT4.Core.Utils;
using Microsoft.Maui.Controls.Xaml.Diagnostics;

namespace GT4.UI;

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

    BindingDiagnostics.BindingFailed += (sender, args) =>
    {
      switch (args)
      {
        case BindingErrorEventArgs bindingError:
          System.Diagnostics.Debug.WriteLine(
            $"[BindingDiagnostics] {string.Format(bindingError.Message, bindingError.MessageArgs)}, "
          + $"Path={(bindingError.Binding as Binding)?.Path ?? "unknown"}, "
          + $"Source='{bindingError.Source}', "
          + $"Target='{bindingError.Target}', "
          + $"Property='{bindingError.TargetProperty.PropertyName}'");
          break;

        default:
          System.Diagnostics.Debug.WriteLine($"[BindingDiagnostics] {args.Message}, Raw='{args}");
          break;
      }
    };


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
