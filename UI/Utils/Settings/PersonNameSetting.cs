using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class PersonNameSetting : ISettingEditor
{

  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly string _FormatSection;
  private readonly string _DefaultFormat;
  private readonly Func<string> _DisplayName;

  public PersonNameSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    [ServiceKey] NameFormat nameFormat)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    // The localized slot stays deferred: the container keeps the setting as a singleton across a
    // language switch.
    (_FormatSection, _DefaultFormat, _DisplayName) = nameFormat switch
    {
      NameFormat.CommonPersonName => ("NameFormatter.CommonPersonName", "FF PP LL", (Func<string>)(() => UIStrings.FieldCommonPersonNameFormat)),
      NameFormat.FullPersonName => ("NameFormatter.FullPersonName", "FF PP LL (FN)", (Func<string>)(() => UIStrings.FieldFullPersonNameFormat)),
      NameFormat.PersonInitials => ("NameFormatter.PersonInitialsSetting", "LL FF. PP.", (Func<string>)(() => UIStrings.FieldPersonInitialsFormat)),
      NameFormat.ShortPersonName => ("NameFormatter.ShortPersonNameSetting", "FF PP", (Func<string>)(() => UIStrings.ShortPersonNameFormat)),
      _ => throw new ArgumentOutOfRangeException(nameof(nameFormat), nameFormat, null)
    };
  }

  public string DisplayName => _DisplayName();

  public string Example => NameFormatter.Format(Value, _PersonInfo);

  public string Description => UIStrings.FieldPersonNameFormatHint;

  public string Group => nameof(NameFormatter);

  public string Value
  {
    get => _Configuration[_FormatSection] ?? _DefaultFormat;
    set => _InteractiveConfiguration?.SetKey(_FormatSection, value);
  }

  public void ResetToDefault() => _InteractiveConfiguration?.RemoveKey(_FormatSection);
  private static PersonInfo _PersonInfo => new PersonInfo(
    Id: 0,
    BirthDate: Date.Now,
    DeathDate: null,
    BiologicalSex: BiologicalSex.Unknown,
    Names: [
      new Name(Id: 0, UIStrings.NameFirst, NameType.FirstName, null),
      new Name(Id: 0, UIStrings.NamePatronymic, NameType.Patronymic, null),
      new Name(Id: 0, UIStrings.NameLast, NameType.LastName, null),
      new Name(Id: 0, UIStrings.NameFamily, NameType.FamilyName, null) ],
      MainPhoto: null);
}
