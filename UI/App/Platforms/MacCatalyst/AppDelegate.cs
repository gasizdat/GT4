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
        // global: Cmd +/- adjust the font scale and Cmd 0 resets it, mirroring the Windows Ctrl hotkeys.
        // NOTE: authored on Windows; not yet verified on a Mac build.
        private readonly UIKeyCommand[] _FontScaleCommands =
        [
            UIKeyCommand.Create("=", UIKeyModifierFlags.Command, new Selector("onFontScaleIncrease:")),
            UIKeyCommand.Create("-", UIKeyModifierFlags.Command, new Selector("onFontScaleDecrease:")),
            UIKeyCommand.Create("0", UIKeyModifierFlags.Command, new Selector("onFontScaleReset:")),
        ];

        public override UIKeyCommand[] KeyCommands => _FontScaleCommands;

        [Export("onFontScaleIncrease:")]
        public void OnFontScaleIncrease(UIKeyCommand command) => CurrentApp?.StepFontScale(FontScale.Step);

        [Export("onFontScaleDecrease:")]
        public void OnFontScaleDecrease(UIKeyCommand command) => CurrentApp?.StepFontScale(-FontScale.Step);

        [Export("onFontScaleReset:")]
        public void OnFontScaleReset(UIKeyCommand command) => CurrentApp?.ResetFontScale();

        private static App? CurrentApp => Microsoft.Maui.Controls.Application.Current as App;
    }
}
