using GT4.Core.Project.Dto;

namespace GT4.UI.Formatters;

internal class NameFormatter : INameFormatter
{
  private readonly static string _PartsDelimiter = " ";

  protected static IEnumerable<Name> GetNameParts(PersonInfo personInfo, NameType nameType)
  {
    return personInfo
      .Names
      .Where(n => (n.Type & nameType) == nameType);
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
      .ToArray();
    return parts;
  }

  protected static string GetFullPersonName(PersonInfo personInfo)
  {
    //TODO use settings
    var parts = GetNameParts(personInfo, [NameType.FirstName, NameType.MiddleName, NameType.LastName, NameType.AdditionalName]);
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

  protected static string GetPersonInitials(PersonInfo personInfo)
  {
    throw new NotImplementedException();
  }

  public string ToString(PersonInfo personInfo, NameFormat format)
  {
    return format switch
    {
      NameFormat.CommonPersonName => GetCommonPersonName(personInfo),
      NameFormat.FullPersonName => GetFullPersonName(personInfo),
      NameFormat.PersonInitials => GetPersonInitials(personInfo),
      _ => throw new ArgumentException(nameof(format))

    };
  }
}
