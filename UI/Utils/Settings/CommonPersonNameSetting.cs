using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal class CommonPersonNameSetting : CommonPersonNameSettingBase, ISettingEditor
{
  protected override string PersonNameFormatSection => "NameFormatter.CommonPersonName";
  protected override string DefaultPersonNameFormat => "FF PP LL";

  public CommonPersonNameSetting(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
    : base (serviceProvider, configuration, interactiveConfiguration, NameFormat.CommonPersonName)
  {
  }

  public string DisplayName => UIStrings.FieldCommonPersonNameFormat;
}
