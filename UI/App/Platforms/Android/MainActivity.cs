using Android.App;
using Android.Content.PM;
using Android.OS;

namespace GT4
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
  {
    protected override void OnCreate(Bundle? savedInstanceState)
    {
      base.OnCreate(savedInstanceState);

      // ✅ This is crucial for MAUI Essentials to work properly
      //Microsoft.Maui.ApplicationModel.Platform.Init(this);
    }

    // ✅ Also add this override to support permission requests
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
      base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
      Microsoft.Maui.ApplicationModel.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }

  }
}
