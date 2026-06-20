using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views;
using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI;
using GT4.UI.Pages;

namespace GT4
{
  [Activity(
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
        [Intent.ActionView],
        Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
        DataMimeTypes = [IProjectDocument.MimeType, "application/gt4", "application/octet-stream", "*/*"],
        DataSchemes = ["content", "file"],
        DataPathPatterns = [".*\\.gt4", ".*\\.GT4"]
  )]
  public class MainActivity : MauiAppCompatActivity
  {
    private readonly IServiceProvider _Services;
    private ScaleGestureDetector? _ScaleDetector;

    protected MainActivity(IServiceProvider serviceProvider)
    {
      _Services = serviceProvider;
    }

    public MainActivity()
      : this(GT4Services.Provider)
    {

    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
      base.OnCreate(savedInstanceState);
      _ScaleDetector = new ScaleGestureDetector(this, new FontScaleGestureListener());
      HandleOpenIntentIfAny(Intent);
    }

    // Feed every touch to the scale detector so a two-finger pinch zooms the global font scale from
    // any page. The detector only reacts to multi-touch, and we always forward the event, so normal
    // single-finger scrolling and taps are untouched.
    public override bool DispatchTouchEvent(MotionEvent? e)
    {
      if (e is not null)
      {
        _ScaleDetector?.OnTouchEvent(e);
      }

      return base.DispatchTouchEvent(e);
    }

    // Bridges Android's pinch gesture to the shared App font-scale helpers. Touch dispatch runs on the
    // UI thread, so applying the scale (which touches Application.Current.Resources) is safe here.
    private sealed class FontScaleGestureListener : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
      private static App? CurrentApp => Microsoft.Maui.Controls.Application.Current as App;

      public override bool OnScale(ScaleGestureDetector detector)
      {
        CurrentApp?.UpdateFontScaleGesture(detector.ScaleFactor);
        return base.OnScale(detector);
      }
    }

    protected override void OnNewIntent(Intent? intent)
    {
      base.OnNewIntent(intent);
      HandleOpenIntentIfAny(intent);
    }

    private async Task ImportProjectAsync(Android.Net.Uri uri)
    {
      try
      {
        using var input = ContentResolver?.OpenInputStream(uri) ??
          throw new ApplicationException($"Unable to open provided URI {uri}");

        using var token = _Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
        await _Services.GetRequiredService<IProjectList>().ImportAsync(input, token);

        RunOnUiThread(() => _ = Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectListPage>()));
      }
      catch (Exception ex)
      {
        await PageAlert.ShowErrorAsync(ex);
      }
    }

    private void HandleOpenIntentIfAny(Intent? intent)
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

      // Persistable is only valid for SAF (DocumentsProvider) + flag present on the incoming intent
      var isDocumentUri = DocumentsContract.IsDocumentUri(this, uri);
      var hasPersistableGrant = intent.Flags.HasFlag(ActivityFlags.GrantPersistableUriPermission);

      if (!isDocumentUri || !hasPersistableGrant)
      {
        Task.Run(() => ImportProjectAsync(uri));
      }
      else
      {
        var takeFlags = intent.Flags & (ActivityFlags.GrantReadUriPermission |
                                        ActivityFlags.GrantWriteUriPermission |
                                        ActivityFlags.GrantPersistableUriPermission);
        ContentResolver?.TakePersistableUriPermission(uri, takeFlags);

        // TODO remove the else section or implement it
        throw new NotImplementedException(nameof(HandleOpenIntentIfAny));
      }
    }
  }
}
