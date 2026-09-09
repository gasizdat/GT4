using GT4.Core.Project.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Utils.Settings;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.System;
using KeyboardAccelerator = Microsoft.UI.Xaml.Input.KeyboardAccelerator;

namespace GT4.UI;

public partial class App
{
  // Wire Ctrl +/- (and Ctrl 0 to reset) to zoom. Accelerators are attached to the native window's
  // root content so they fire from any page regardless of focus.
  partial void RegisterZoomHotkeys(Microsoft.Maui.Controls.Window window) => window.HandlerChanged += (_, _) => AttachAccelerators(window);

  // Mirrors MainActivity's Android handler: land a double-clicked .gt4 file as a new project and
  // show the list. Runs once per launch -- there is no OnNewIntent equivalent here, since a second
  // double-click while running just opens a second instance.
  //
  // The activating path can arrive two ways, so both are read rather than assumed: the unpackaged
  // build is registry-launched, so it comes as a plain argv entry; the MSIX build's manifest-declared
  // association is expected to come through AppInstance's activation args instead, since this is a
  // WinUI/Windows App SDK Application rather than a classic Win32 entry point.
  partial void HandleFileActivation() => _ = ImportActivationFileAsync();

  private async Task ImportActivationFileAsync()
  {
    ProjectFileAssociation.EnsureRegisteredIfUnpackaged();

    var path = GetActivationFilePath();
    if (path is null)
    {
      return;
    }

    try
    {
      using var content = File.OpenRead(path);
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _ProjectList.ImportAsync(content, token);
      await _NavigationService.GoToAsync(UIRoutes.GetRoute<ProjectListPage>());
    }
    catch (Exception ex)
    {
      // Same rationale as the other lifecycle handlers in App.xaml.cs: this runs during window
      // creation, before any page exists to show an alert.
      WriteErrorLog(ex.ToString());
    }
  }

  private static string? GetActivationFilePath()
  {
    var args = Environment.GetCommandLineArgs();
    if (args.Length == 2 && IsProjectFile(args[1]))
    {
      return args[1];
    }

    var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
    if (activationArgs?.Kind == ExtendedActivationKind.File
      && activationArgs.Data is FileActivatedEventArgs fileArgs
      && fileArgs.Files.FirstOrDefault()?.Path is string activatedPath
      && IsProjectFile(activatedPath))
    {
      return activatedPath;
    }

    return null;
  }

  private static bool IsProjectFile(string path) =>
    string.Equals(Path.GetExtension(path), ProjectFileExtensions.Gt4Extension, StringComparison.OrdinalIgnoreCase);

  private void AttachAccelerators(Microsoft.Maui.Controls.Window window)
  {
    if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native)
    {
      return;
    }

    if (native.Content is FrameworkElement root)
    {
      AttachTo(root);
      return;
    }

    // The root content may not be set yet when the handler first connects; retry on activation.
    void OnActivated(object sender, WindowActivatedEventArgs e)
    {
      if (native.Content is FrameworkElement ready)
      {
        native.Activated -= OnActivated;
        AttachTo(ready);
      }
    }

    native.Activated += OnActivated;
  }

  private void AttachTo(FrameworkElement root)
  {
    // HandlerChanged can fire more than once for the same content; don't stack duplicate accelerators.
    if (root.KeyboardAccelerators.Count > 0)
    {
      return;
    }

    // By default WinUI advertises each accelerator in a tooltip on the owning element; since these
    // live on the root content that tooltip would pop up on hover anywhere in the window. Hide it.
    root.KeyboardAcceleratorPlacementMode = Microsoft.UI.Xaml.Input.KeyboardAcceleratorPlacementMode.Hidden;

    // Both the main-row '='/'-' keys and the numpad +/- so either gesture works.
    Add(root, VirtualKey.Add, () => StepZoom(FontScale.Step));
    Add(root, (VirtualKey)0xBB /* OemPlus '=' */, () => StepZoom(FontScale.Step));
    Add(root, VirtualKey.Subtract, () => StepZoom(-FontScale.Step));
    Add(root, (VirtualKey)0xBD /* OemMinus '-' */, () => StepZoom(-FontScale.Step));
    Add(root, VirtualKey.Number0, ResetZoom);
    Add(root, VirtualKey.NumberPad0, ResetZoom);
  }

  private static void Add(FrameworkElement root, VirtualKey key, Action action)
  {
    var accelerator = new KeyboardAccelerator
    {
      Modifiers = VirtualKeyModifiers.Control,
      Key = key,
    };
    accelerator.Invoked += (_, args) =>
    {
      action();
      args.Handled = true;
    };
    root.KeyboardAccelerators.Add(accelerator);
  }
}
