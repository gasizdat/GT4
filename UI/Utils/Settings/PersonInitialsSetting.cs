using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal class PersonInitialsSetting : CommonPersonNameSettingBase, ISettingEditor
{
  protected override string PersonNameFormatSection => "NameFormatter.PersonInitialsSetting";
  protected override string DefaultPersonNameFormat => "LL FF. PP.";

  public PersonInitialsSetting(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration)
    : base (serviceProvider, configuration, interactiveConfiguration, NameFormat.PersonInitials)
  {
  }

  public string DisplayName => UIStrings.FieldPersonInitialsFormat;
}
