using Android.App;
using Android.Content.PM;

namespace GT4.UI.DeviceTests;

[Activity(
  // The DeviceRunners CLI launches "<ApplicationId>/.MainActivity"; without an explicit Name the
  // generated Android activity name is namespace-mangled and that intent resolves to nothing.
  Name = "com.gasizdat.gt4.devicetests.MainActivity",
  Theme = "@style/Maui.SplashTheme",
  MainLauncher = true,
  LaunchMode = LaunchMode.SingleTask,
  ConfigurationChanges = ConfigChanges.ScreenSize
                       | ConfigChanges.Orientation
                       | ConfigChanges.UiMode
                       | ConfigChanges.ScreenLayout
                       | ConfigChanges.SmallestScreenSize
                       | ConfigChanges.Density
)]
public class MainActivity : MauiAppCompatActivity
{
}
