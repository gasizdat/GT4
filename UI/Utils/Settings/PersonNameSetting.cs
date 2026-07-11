using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

internal sealed class PersonNameSetting : ISettingEditor
{
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

  private readonly IServiceProvider _ServiceProvider;
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly NameFormat _NameFormat;
  private readonly string _FormatSection;
  private readonly string _DefaultFormat;

  public PersonNameSetting(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IInteractiveConfiguration? interactiveConfiguration,
    NameFormat nameFormat,
    string formatSection,
    string defaultFormat,
    string displayName)
  {
    _ServiceProvider = serviceProvider;
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
    _NameFormat = nameFormat;
    _FormatSection = formatSection;
    _DefaultFormat = defaultFormat;
    DisplayName = displayName;
  }

  public string DisplayName { get; }

  public string Example => _ServiceProvider
    .GetRequiredService<INameFormatter>()
    .ToString(_PersonInfo, _NameFormat);

  public string Description => UIStrings.FieldPersonNameFormatHint;

  public string Group => nameof(NameFormatter);

  public string Value
  {
    get => _Configuration[_FormatSection] ?? _DefaultFormat;
    set => _InteractiveConfiguration?.SetKey(_FormatSection, value);
  }

  public void ResetToDefault()
  {
    _InteractiveConfiguration?.RemoveKey(_FormatSection);
  }
}
