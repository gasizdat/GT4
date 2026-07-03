using DeviceRunners.UITesting;
using DeviceRunners.VisualRunners;
using GT4.UI;

namespace GT4.UI.DeviceTests;

public static class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
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
