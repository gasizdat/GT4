using GT4.UI.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Logging;
using System.Text;

namespace GT4.UI;

public static class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
    // Needed for GEDCOM imports that resolve a legacy codepage (e.g. Windows-1251) for an ANSI-declared file.
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .ConfigureFonts(fonts =>
        {
          fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
          fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });

#if DEBUG
    builder.Logging.AddDebug();
#endif

    GT4Services.Add(builder.Services);

    var app = builder.Build();

    // Apply the persisted language before the Shell and its pages are created so the whole UI renders
    // in the saved language from the first frame.
    Language.Current = app.Services.GetRequiredService<LanguageSetting>().Value;

    return app;
  }
}
