using GT4.Core.Project;
using GT4.Core.Utils;
using GT4.UI.Utils;
using Microsoft.Extensions.Configuration;

namespace GT4.UI;

public class GT4Services
{
  public static void Add(IServiceCollection serviceCollection)
  {
    var configurationRoot = new ConfigurationBuilder()
      .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
      .AddAppConfiguration()
      .Build();

    serviceCollection
      .AddSingleton<IConfiguration>(configurationRoot)
      .AddActiveConfigurations(configurationRoot)
      .AddUIUtils()
      .AddDefaultUtils()
      .AddDefaultProject();
  }

  public static IServiceProvider Provider =>
    IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("DI container not available.");
}
