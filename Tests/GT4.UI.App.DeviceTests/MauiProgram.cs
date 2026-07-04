using DeviceRunners.UITesting;
using DeviceRunners.VisualRunners;

namespace GT4.UI.DeviceTests;

public static class MauiProgram
{
#if WINDOWS
  [System.Runtime.InteropServices.DllImport("user32.dll")]
  private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
#endif
  public static MauiApp CreateMauiApp()
  {
#if DEBUG
    // Fails fast with a readable message instead of the XamlParseException MAUI's Debug/Hot
    // Reload XAML resolution throws when a page is hosted from a referenced assembly (see the
    // note at the top of GT4.UI.App.DeviceTests.csproj). A thrown exception here would cross into
    // WinUI's native message pump and surface only as an opaque access-violation-style crash with
    // no readable text, so exit directly instead: Environment.Exit terminates before any MAUI
    // infrastructure starts, and Console.Error is flushed synchronously first.

    const string ErrorMessage = "GT4.UI.App.DeviceTests does not support Debug configuration. Build and run with -c Release instead.";
    Console.Error.WriteLine(ErrorMessage);
    Console.Error.Flush();
#if WINDOWS
    MessageBox(IntPtr.Zero, ErrorMessage, "Error", 0x10); // 0x10 = MB_ICONERROR
#endif
    Environment.Exit(1);
#endif

    var builder = MauiApp.CreateBuilder();
    builder
      .ConfigureUITesting()
      .ConfigureFonts(fonts =>
      {
        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
      })
      .UseVisualTestRunner(conf => conf
        .AddCliConfiguration()
        .AddConsoleResultChannel()
        .AddTestAssembly(typeof(MauiProgram).Assembly)
        .AddXunit3());

    // Safety net for parameterless view ctors (NameView etc.) that resolve GT4Services.Provider
    // when a DataTemplate is realized; tests build their own isolated providers.
    GT4Services.Add(builder.Services);

    return builder.Build();
  }
}
