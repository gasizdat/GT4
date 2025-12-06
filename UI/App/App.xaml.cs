using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using Microsoft.Maui.Controls.Xaml.Diagnostics;
using System.Globalization;

namespace GT4.UI;

public partial class App : Application
{
  private readonly IServiceProvider _Services;

  protected App(IServiceProvider serviceProvider)
  {
    _Services = serviceProvider;
    InitializeComponent();

    AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
    {
      string errorMessage = e.ExceptionObject switch
      {
        Exception exception => exception.ToString(),
        _ => e.ExceptionObject.ToString() ?? "Undefined error"
      };

      var logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GT4",
        "CrashLogs");
      Directory.CreateDirectory(logDir);

      var time = DateTime.Now;
      var logName = $"gt4-run-{time.ToString("o", CultureInfo.InvariantCulture)}.log"
        .Replace(':', '-')
        .Replace('\\', '-')
        .Replace('/', '-');

      using var fileStream = File.OpenWrite(Path.Combine(logDir, logName));
      using var writer = new StreamWriter(fileStream);
      writer.Write(errorMessage);
      fileStream.Flush();
      fileStream.Close();
    };

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

  public App()
    : this(ServiceBuilder.DefaultServices)
  {

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
    using var token = _Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
    var projectProvider = _Services.GetRequiredService<ICurrentProjectProvider>();
    if (projectProvider.HasCurrentProject)
    {
      await projectProvider.UpdateOriginAsync(token);
    }
  }

  private async void SaveOnDisposeAsync(object? sender, EventArgs e)
  {
    using var token = _Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
    var projectProvider = _Services.GetRequiredService<ICurrentProjectProvider>();
    if (projectProvider.HasCurrentProject)
    {
      await projectProvider.CloseAsync(token);
    }
  }
}
