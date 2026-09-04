using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Settings;
using Microsoft.Maui.Controls.Xaml.Diagnostics;
using System.Globalization;

namespace GT4.UI;

public partial class App : Application
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly FontScale _FontScale;
  private readonly ISettingEditor? _FontScaleSetting;
  private readonly IInteractiveConfiguration? _AppConfiguration;

  // The Activated/Deactivated/Destroying handlers are fire-and-forget for MAUI, so a quick
  // deactivate/activate could otherwise reopen the project while the close is still flushing the
  // cache file back to the origin. This lock keeps the open/close sequences strictly ordered.
  private readonly SemaphoreSlim _LifecycleLock = new(1, 1);
  private ProjectInfo? _LastOpenedProject;

  public App(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    [FromKeyedServices(SettingKeys.FontScale)]
    ISettingEditor? fontScaleSetting,
    FontScale fontScale,
    [FromKeyedServices(SettingKeys.BackgroundAnimation)]
    ISettingEditor? backgroundAnimationSetting,
    BackgroundAnimation backgroundAnimation,
    [FromKeyedServices(SettingKeys.Theme)]
    ISettingEditor? themeSetting,
    Theme theme,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? appConfiguration)
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _FontScale = fontScale;
    _FontScaleSetting = fontScaleSetting;
    _AppConfiguration = appConfiguration;

    AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
    BindingDiagnostics.BindingFailed += LogBindingErrors;

    InitializeComponent();

    fontScale.Initialize();
    fontScale.Apply(fontScaleSetting?.Value);
    backgroundAnimation.Apply(backgroundAnimationSetting?.Value);
    theme.Apply(themeSetting?.Value);

#if ANDROID
    Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.UseWindowSoftInputModeAdjust(
      Current?.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>(),
      Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif
  }

  protected static void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
  {
    var errorMessage = e.ExceptionObject switch
    {
      Exception exception => exception.ToString(),
      _ => e.ExceptionObject.ToString() ?? "Undefined error"
    };

    WriteErrorLog(errorMessage);
  }

  private static void WriteErrorLog(string errorMessage)
  {
    System.Diagnostics.Debug.WriteLine(errorMessage);

    try
    {
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

      File.WriteAllText(Path.Combine(logDir, logName), errorMessage);
    }
    catch (Exception logFailure)
    {
      // Logging is best effort: never let a failure writing the log mask the original error or, when
      // called from a lifecycle handler, escape and crash the app.
      System.Diagnostics.Debug.WriteLine(logFailure);
    }
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
#if ANDROID || IOS
    // Backgrounding can kill the process, so the project must not stay open across it.
    window.Activated += ReopenOnActivationAsync;
    window.Deactivated += async (_, _) => await CloseOnDeactivationAsync(saveLastOpenProject: true);
#else
    // A desktop window deactivates on mere focus loss, including while a native file picker is up --
    // before PickAsync returns -- so closing the project here would tear it down mid-operation.
    window.Deactivated += (_, _) => _AppConfiguration?.Flush();
#endif
    window.Destroying += async (_, _) => await CloseOnDeactivationAsync(saveLastOpenProject: false);
    RegisterZoomHotkeys(window);
    return window;
  }

  // Implemented per platform (Windows). On platforms without a keyboard this compiles away.
  partial void RegisterZoomHotkeys(Window window);

  internal void StepZoom(double delta)
  {
    if (CurrentZoomablePage is { } page)
    {
      page.Zoom(delta);
      return;
    }

    if (_FontScaleSetting is null)
    {
      return;
    }

    var newScaleFactor = (int)Math.Round(100 * (_FontScale.CurrentFactor + delta));
    _FontScaleSetting.Value = $"{newScaleFactor}%";
  }

  internal void ResetZoom()
  {
    if (CurrentZoomablePage is { } page)
    {
      page.ResetZoom();
      return;
    }

    _FontScaleSetting?.ResetToDefault();
  }

  private static IZoomablePage? CurrentZoomablePage => Shell.Current?.CurrentPage as IZoomablePage;

  private async void ReopenOnActivationAsync(object? sender, EventArgs e)
  {
#if DEBUG
    if (System.Diagnostics.Debugger.IsAttached)
    {
      return;
    }
#endif

    await _LifecycleLock.WaitAsync();
    try
    {
      if (_LastOpenedProject is null)
      {
        return;
      }

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _CurrentProjectProvider.OpenAsync(_LastOpenedProject, token);
    }
    catch (Exception ex)
    {
      // This is an async void lifecycle handler: an escaped exception is posted to the platform
      // loop (on Android it terminates the process). There is no UI to surface a failure to during
      // a lifecycle transition, so log and swallow it instead of crashing.
      WriteErrorLog(ex.ToString());
    }
    finally
    {
      _LifecycleLock.Release();
    }
  }

  private async Task CloseOnDeactivationAsync(bool saveLastOpenProject)
  {
    // Persist any debounced settings (e.g. a just-changed font scale) before backgrounding/teardown,
    // so a change made within the debounce window is not lost. Done before the debugger short-circuit
    // below so settings still persist while debugging.
    _AppConfiguration?.Flush();

#if DEBUG
    if (System.Diagnostics.Debugger.IsAttached)
    {
      return;
    }
#endif

    await _LifecycleLock.WaitAsync();
    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      _LastOpenedProject = null;
      if (_CurrentProjectProvider.HasCurrentProject)
      {
        if (saveLastOpenProject)
        {
          _LastOpenedProject = _CurrentProjectProvider.Info;
        }
        await _CurrentProjectProvider.CloseAsync(token);
      }
    }
    catch (Exception ex)
    {
      // This is an async void lifecycle handler (and CloseAsync now throws when the cache flush to
      // the origin fails — likely on Android when the origin URI is briefly inaccessible while
      // backgrounding). An escaped exception is posted to the platform loop and terminates the app,
      // so log and swallow it. The edited cache is preserved on disk, so the data is recoverable.
      WriteErrorLog(ex.ToString());
    }
    finally
    {
      _LifecycleLock.Release();
    }
  }
}
