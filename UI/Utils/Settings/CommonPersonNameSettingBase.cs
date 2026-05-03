using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Settings;

abstract class CommonPersonNameSettingBase
{
  private readonly NameFormat _NameFormat;
  private static PersonInfo _PersonInfo => new PersonInfo(
    Id: 0,
    BirthDate: Date.Now,
    DeathDate: null,
    BiologicalSex: BiologicalSex.Unknown,
    Names: [
      new Name(Id: 0, UIStrings.NameAdditional, NameType.AdditionalName, null),
      new Name(Id: 0, UIStrings.NameFirst, NameType.FirstName, null),
      new Name(Id: 0, UIStrings.NamePatronymic, NameType.Patronymic, null),
      new Name(Id: 0, UIStrings.NameLast, NameType.LastName, null) ],
      MainPhoto: null);
  private readonly IConfiguration _Configuration;
  private readonly IInteractiveConfiguration? _InteractiveConfiguration;
  private readonly IServiceProvider _ServiceProvider;

  protected CommonPersonNameSettingBase(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IInteractiveConfiguration? interactiveConfiguration,
    NameFormat nameFormat)
  {
    _NameFormat = nameFormat;
    _ServiceProvider = serviceProvider;
    _Configuration = configuration;
    _InteractiveConfiguration = interactiveConfiguration;
  }

  protected abstract string PersonNameFormatSection { get; }

  protected abstract string DefaultPersonNameFormat { get; }

  public string Example => _ServiceProvider
    .GetRequiredService<INameFormatter>()
    .ToString(_PersonInfo, _NameFormat);

  public string Description => UIStrings.FieldPersonNameFormatHint;

  public string Group => nameof(NameFormatter);

  public string Value
  {
    get => _Configuration[PersonNameFormatSection] ?? DefaultPersonNameFormat;
    set => _InteractiveConfiguration?.SetKey(PersonNameFormatSection, value);
  }
}