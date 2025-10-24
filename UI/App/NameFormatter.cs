using GT4.Core.Project.Dto;

namespace GT4.UI;

internal class NameFormatter : INameFormatter
{
  private readonly string _PartsDelimiter = " ";

  protected static IEnumerable<Name> GetNameParts(Person person, NameType nameType)
  {
    return person
      .Names
      .Where(n => (n.Type & nameType) == nameType);
  }

  protected static string GetNameValue(Name? name)
  {
    return name is not null ? name.Value : string.Empty;
  }

  protected static string[] GetNameParts(Person person, NameType[] types)
  {
    var parts = types
      .SelectMany(type => GetNameParts(person, type))
      .Select(GetNameValue)
      .ToArray();
    return parts;
  }

  public string GetFullPersonName(Person person)
  {
    //TODO use settings
    var parts = GetNameParts(person, [NameType.FirstName, NameType.MiddleName, NameType.LastName, NameType.AdditionalName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  public string GetCommonPersonName(Person person)
  {
    //TODO use settings
    var parts = GetNameParts(person, [NameType.FirstName, NameType.MiddleName, NameType.LastName]);
    var ret = string.Join(_PartsDelimiter, parts);

    return ret;
  }

  public string GetPersonInitials(Person person)
  {
    throw new NotImplementedException();
  }
}
