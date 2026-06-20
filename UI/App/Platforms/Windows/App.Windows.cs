using GT4.UI.Utils.Settings;
using Microsoft.UI.Xaml;
using Windows.System;
using KeyboardAccelerator = Microsoft.UI.Xaml.Input.KeyboardAccelerator;

namespace GT4.UI;

// Windows-only: wire Ctrl +/- (and Ctrl 0 to reset) to the global font scale. Accelerators are
// attached to the native window's root content so they fire from any page regardless of focus.
public partial class App
{
  partial void RegisterFontScaleHotkeys(Microsoft.Maui.Controls.Window window)
  {
    window.HandlerChanged += (_, _) => AttachAccelerators(window);
  }

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
    Add(root, VirtualKey.Add, () => StepFontScale(FontScale.Step));
    Add(root, (VirtualKey)0xBB /* OemPlus '=' */, () => StepFontScale(FontScale.Step));
    Add(root, VirtualKey.Subtract, () => StepFontScale(-FontScale.Step));
    Add(root, (VirtualKey)0xBD /* OemMinus '-' */, () => StepFontScale(-FontScale.Step));
    Add(root, VirtualKey.Number0, ResetFontScale);
    Add(root, VirtualKey.NumberPad0, ResetFontScale);
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
