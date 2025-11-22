using GT4.Core.Project.Dto;

namespace GT4.UI.Formatters;

internal class NameFormatter : INameFormatter
{
  private readonly static string _PartsDelimiter = " ";
  private readonly static char _InitialSuffix = '.';
  private const NameType _Initials = (NameType)0x10000;

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
    var parts = GetNameParts(personInfo, [NameType.FirstName, NameType.AdditionalName, NameType.MiddleName, NameType.LastName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  protected static string GetCommonPersonName(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.FirstName, NameType.MiddleName, NameType.LastName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  protected static string GetShortPersonName(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.FirstName, NameType.MiddleName | _Initials, NameType.LastName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  protected static string GetPersonInitials(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.LastName, NameType.FirstName | _Initials, NameType.MiddleName | _Initials]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  public string ToString(PersonInfo personInfo, NameFormat format)
  {
    return format switch
    {
      NameFormat.CommonPersonName => GetCommonPersonName(personInfo),
      NameFormat.FullPersonName => GetFullPersonName(personInfo),
      NameFormat.PersonInitials => GetPersonInitials(personInfo),
      NameFormat.ShortPersonName => GetShortPersonName(personInfo),
      _ => throw new ArgumentException(nameof(format))

    };
  }
}
