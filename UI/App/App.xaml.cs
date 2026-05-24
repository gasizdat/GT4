using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Microsoft.Maui.Controls.Xaml.Diagnostics;
using System.Globalization;

namespace GT4.UI;

public partial class App : Application
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private ProjectInfo? _LastOpenedProject;

  public App(ICancellationTokenProvider cancellationTokenProvider, ICurrentProjectProvider currentProjectProvider)
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;

    AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
    BindingDiagnostics.BindingFailed += LogBindingErrors;

    InitializeComponent();

#if ANDROID
    Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.UseWindowSoftInputModeAdjust(
      Current?.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>(),
      Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif
  }

  protected static void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
  {
    string errorMessage = e.ExceptionObject switch
    {
      Exception exception => exception.ToString(),
      _ => e.ExceptionObject.ToString() ?? "Undefined error"
    };

    System.Diagnostics.Debug.WriteLine(errorMessage);

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
  }

  protected static void LogBindingErrors(object? sender, BindingBaseErrorEventArgs args)
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
  }

  protected override Window CreateWindow(IActivationState? activationState)
  {
    var window = new Window(new AppShell());
    window.Activated += ReopenOnActivationAsync;
    window.Deactivated += UpdateOnDeactivationAsync;
    window.Stopped += UpdateOnDeactivationAsync;
    window.Destroying += CloseOnDisposeAsync;
    return window;
  }

  private async void ReopenOnActivationAsync(object? sender, EventArgs e)
  {
    if (_LastOpenedProject is null)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider.OpenAsync(_LastOpenedProject, token);
  }

  private async void UpdateOnDeactivationAsync(object? sender, EventArgs e)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    if (_CurrentProjectProvider.HasCurrentProject)
    {
      _LastOpenedProject = _CurrentProjectProvider.Info;
      await _CurrentProjectProvider.CloseAsync(token);
    }
  }

  private async void CloseOnDisposeAsync(object? sender, EventArgs e)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    if (_CurrentProjectProvider.HasCurrentProject)
    {
      _LastOpenedProject = null;
      await _CurrentProjectProvider.CloseAsync(token);
    }
  }
}
