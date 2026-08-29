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
  private readonly NameFormat _NameFormat;
  private readonly string _FormatSection;
  private readonly string _DefaultFormat;

  public PersonNameSetting(
    IConfiguration configuration,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)]
    IInteractiveConfiguration? interactiveConfiguration,
    [ServiceKey] NameFormat nameFormat)
  {
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _NameFormat = nameFormat;
    (_FormatSection, _DefaultFormat) = nameFormat switch
    {
      NameFormat.CommonPersonName => ("NameFormatter.CommonPersonName", "FF PP LL"),
      NameFormat.FullPersonName => ("NameFormatter.FullPersonName", "FF PP LL (FN)"),
      NameFormat.PersonInitials => ("NameFormatter.PersonInitialsSetting", "LL FF. PP."),
      NameFormat.ShortPersonName => ("NameFormatter.ShortPersonNameSetting", "FF PP"),
      _ => throw new ArgumentOutOfRangeException(nameof(nameFormat), nameFormat, null)
    };
  }

  // Resolved per read: the container keeps the setting as a singleton across a language switch.
  public string DisplayName => _NameFormat switch
  {
    NameFormat.CommonPersonName => UIStrings.FieldCommonPersonNameFormat,
    NameFormat.FullPersonName => UIStrings.FieldFullPersonNameFormat,
    NameFormat.PersonInitials => UIStrings.FieldPersonInitialsFormat,
    _ => UIStrings.ShortPersonNameFormat
  };

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
