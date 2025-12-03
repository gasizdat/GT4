using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using GT4.Core.Project;
using GT4.Core.Utils;
using GT4.UI;
using GT4.UI.Pages;
using GT4.UI.Resources;
using System.ComponentModel;

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
        DataMimeTypes = [ProjectDocument.MimeType, "application/gt4", "application/octet-stream", "*/*"],
        DataSchemes = ["content", "file"],
        DataPathPatterns = [".*\\.gt4", ".*\\.GT4"]
  )]
  public class MainActivity : MauiAppCompatActivity
  {
    private readonly IServiceProvider _Services;

    protected MainActivity(IServiceProvider serviceProvider)
    {
      _Services = serviceProvider;
    }

    public MainActivity()
      : this(ServiceBuilder.DefaultServices)
    {

    }

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

    private async Task ExportProjectAsync(Uri uri)
    {
      try
      {
        using var input = ContentResolver?.OpenInputStream(uri) ??
          throw new ApplicationException($"Unable to open provided URI {uri}");

        using var token = _Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
        await _Services.GetRequiredService<IProjectList>().ExportAsync(input, token);

        RunOnUiThread(() => _ = Shell.Current.GoToAsync(UIRoutes.GetRoute<ProjectPage>()));
      }
      catch (Exception ex)
      {
        await PageAlert.ShowError(ex);
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
        Task.Run(() => ExportProjectAsync(uri));
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
