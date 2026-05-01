using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Settings;

namespace GT4.UI.Utils.Formatters;

public class NameFormatter : INameFormatter
{
  private readonly ISettingEditor _CommonPersonNameSetting;
  private readonly static string _PartsDelimiter = " ";
  private readonly static char _InitialSuffix = '.';
  private const NameType _Initials = (NameType)0x10000;

  public NameFormatter([FromKeyedServices(nameof(CommonPersonNameSetting))] ISettingEditor commonPersonNameSetting)
  {
    _CommonPersonNameSetting = commonPersonNameSetting;
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

  protected static string GetFullPersonName(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.FirstName, NameType.AdditionalName, NameType.Patronymic, NameType.LastName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  protected static string GetCommonPersonName(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.FirstName, NameType.Patronymic, NameType.LastName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  protected static string GetShortPersonName(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.FirstName, NameType.Patronymic | _Initials, NameType.LastName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  protected static string GetPersonInitials(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.LastName, NameType.FirstName | _Initials, NameType.Patronymic | _Initials]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  public string ToString(PersonInfo personInfo, NameFormat format)
  {
    var template = format switch
    {
      NameFormat.CommonPersonName => _CommonPersonNameSetting.Value,
      NameFormat.FullPersonName => GetFullPersonName(personInfo),
      NameFormat.PersonInitials => GetPersonInitials(personInfo),
      NameFormat.ShortPersonName => GetShortPersonName(personInfo),
      _ => throw new ArgumentException(nameof(format))
    };

    string GetNames(NameType nameType)
    {
      var ret = string.Join(_PartsDelimiter, [.. GetNameParts(personInfo, nameType).Select(n => n.Value)]);
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
