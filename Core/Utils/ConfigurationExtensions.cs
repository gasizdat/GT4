using Microsoft.Extensions.Configuration;

namespace GT4.Core.Utils;

public static class ConfigurationExtensions
{
  public static IConfigurationBuilder AddAppConfiguration(this IConfigurationBuilder configurationBuilder)
  {
    configurationBuilder.Add(new AppConfigurationSource());
    return configurationBuilder;
  }
}
