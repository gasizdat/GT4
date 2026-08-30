using Foundation;
using GT4.UI;
using GT4.UI.Utils.Settings;
using ObjCRuntime;
using UIKit;

namespace GT4
{
  [Register("AppDelegate")]
  public class AppDelegate : MauiUIApplicationDelegate
  {
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // The app delegate is the last link in the responder chain, so key commands declared here are
    // global: Cmd +/- zoom and Cmd 0 resets, mirroring the Windows Ctrl hotkeys.
    // NOTE: authored on Windows; not yet verified on a Mac build.
    private readonly UIKeyCommand[] _ZoomCommands =
    [
        UIKeyCommand.Create("=", UIKeyModifierFlags.Command, new Selector("onZoomIn:")),
            UIKeyCommand.Create("-", UIKeyModifierFlags.Command, new Selector("onZoomOut:")),
            UIKeyCommand.Create("0", UIKeyModifierFlags.Command, new Selector("onZoomReset:")),
        ];

    public override UIKeyCommand[] KeyCommands => _ZoomCommands;

    [Export("onZoomIn:")]
    public void OnZoomIn(UIKeyCommand command) => CurrentApp?.StepZoom(FontScale.Step);

    [Export("onZoomOut:")]
    public void OnZoomOut(UIKeyCommand command) => CurrentApp?.StepZoom(-FontScale.Step);

    [Export("onZoomReset:")]
    public void OnZoomReset(UIKeyCommand command) => CurrentApp?.ResetZoom();

    private static App? CurrentApp => Microsoft.Maui.Controls.Application.Current as App;
  }
}
