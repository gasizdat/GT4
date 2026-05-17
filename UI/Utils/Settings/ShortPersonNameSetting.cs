using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal class ShortPersonNameSetting : CommonPersonNameSettingBase, ISettingEditor
{
  protected override string PersonNameFormatSection => "NameFormatter.ShortPersonNameSetting";
  protected override string DefaultPersonNameFormat => "FF PP";

  public ShortPersonNameSetting(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
    : base (serviceProvider, configuration, interactiveConfiguration, NameFormat.ShortPersonName)
  {
  }

  public string DisplayName => UIStrings.ShortPersonNameFormat;
}
