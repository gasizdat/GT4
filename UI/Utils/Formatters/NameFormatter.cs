using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI.Utils.Formatters;

public class NameFormatter : INameFormatter
{
  private readonly ISettingEditor _CommonPersonNameSetting;
  private readonly ISettingEditor _FullPersonNameSetting;
  private readonly ISettingEditor _PersonInitialsSetting;
  private readonly ISettingEditor _ShortPersonNameSetting;
  private const string _PartsDelimiter = " ";
  private const char _InitialSuffix = '.';
  private const NameType _Initials = (NameType)0x10000;

  public NameFormatter(
    [FromKeyedServices(NameFormat.CommonPersonName)] ISettingEditor commonPersonNameSetting,
    [FromKeyedServices(NameFormat.FullPersonName)] ISettingEditor fullPersonNameSetting,
    [FromKeyedServices(NameFormat.PersonInitials)] ISettingEditor personInitialsSetting,
    [FromKeyedServices(NameFormat.ShortPersonName)] ISettingEditor shortPersonNameSetting)
  {
    _CommonPersonNameSetting = commonPersonNameSetting;
    _FullPersonNameSetting = fullPersonNameSetting;
    _PersonInitialsSetting = personInitialsSetting;
    _ShortPersonNameSetting = shortPersonNameSetting;
  }

  protected static Name GetInitial(Name name)
  {
    var initial = new string([char.ToUpper(name.Value.FirstOrDefault()), _InitialSuffix]);
    return name with { Value = initial };
  }

  protected static IEnumerable<Name> GetNameParts(PersonInfo personInfo, NameType nameType)
  {
    var names = personInfo
      .Names
      .Where(n => n.Type.HasFlag(nameType & ~_Initials));

    if (nameType.HasFlag(_Initials))
    {
      names = names.Select(GetInitial);
    }

    return names;
  }

  protected static string GetNameValue(Name? name)
  {
    return name is not null ? name.Value : string.Empty;
  }

  protected static string[] GetNameParts(PersonInfo personInfo, NameType[] types)
  {
    var parts = types
      .SelectMany(type => GetNameParts(personInfo, type))
      .Select(GetNameValue)
      .Where(name => !string.IsNullOrWhiteSpace(name))
      .ToArray();
    return parts;
  }

  public string ToString(PersonInfo personInfo, NameFormat format)
  {
    var template = format switch
    {
      NameFormat.CommonPersonName => _CommonPersonNameSetting.Value,
      NameFormat.FullPersonName => _FullPersonNameSetting.Value,
      NameFormat.PersonInitials => _PersonInitialsSetting.Value,
      NameFormat.ShortPersonName => _ShortPersonNameSetting.Value,
      _ => throw new ArgumentException(nameof(format))
    };

    string GetNames(NameType nameType)
    {
      var ret = string.Join(_PartsDelimiter, [.. GetNameParts(personInfo, nameType).Select(GetNameValue)]);
      return ret;
    }

    var ret = TemplateInterpolator.Format(template, new Dictionary<string, Func<string>>()
    {
      { "AA", () => GetNames(NameType.AdditionalName)},
      { "FF", () => GetNames(NameType.FirstName)},
      { "PP", () => GetNames(NameType.Patronymic)},
      { "LL", () => GetNames(NameType.LastName)},
      { "AA.", () => GetNames(NameType.AdditionalName | _Initials)},
      { "FF.", () => GetNames(NameType.FirstName | _Initials)},
      { "PP.", () => GetNames(NameType.Patronymic | _Initials)},
      { "LL.", () => GetNames(NameType.LastName | _Initials)},
    });

    return ret;
  }
}
