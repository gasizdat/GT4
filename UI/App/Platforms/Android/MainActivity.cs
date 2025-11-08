using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using GT4.UI.Resources;

namespace GT4
{
  [Activity(
    //Name = "com.gasizdat.gt4.MainActivity", // 👈 force the Java/Android name
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


  // Accept *.gt4 files by MIME and by extension (for generic MIME senders)
  [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataMimeTypes = new[] {
            "application/gt4",
            "application/gt4;storage=sqlite",
            "application/octet-stream",
            "*/*"
        },
        DataSchemes = new[] { "content", "file" },
        DataPathPatterns = new[] { ".*\\.gt4", ".*\\.GT4" }
    )]

  public class MainActivity : MauiAppCompatActivity
  {

    protected override void OnCreate(Bundle? savedInstanceState)
    {
      base.OnCreate(savedInstanceState);
      HandleOpenIntentIfAny(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
      base.OnNewIntent(intent);
      HandleOpenIntentIfAny(intent);
    }

    void HandleOpenIntentIfAny(Intent? intent)
    {
      if (intent?.Action != Intent.ActionView)
      {
        return;
      }

      var uri = intent.Data;
      if (uri is null)
      {
        return;
      }
      // If it’s a content:// Uri, a read grant is typically provided with the Intent.
      // You can persist the grant if needed:
      try
      {
        if ((intent.Flags & ActivityFlags.GrantReadUriPermission) == ActivityFlags.GrantReadUriPermission)
        {
          ContentResolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
        }
      }
      catch (Exception ex)
      {
       // DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);

      }

      // TODO: hand off the Uri to your MAUI code (e.g., MessagingCenter/WeakReferenceMessenger/Singleton)
    }

  }
}
