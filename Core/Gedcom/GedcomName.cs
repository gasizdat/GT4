namespace GT4.Core.Gedcom;

/// <summary>
/// Splits a NAME node into its given/surname parts, preferring the explicit GIVN/SURN sub-tags over the
/// "Given /Surname/" value they duplicate. Anything that has to line up with what import stored -- the
/// merge-import identity, a round-trip comparison -- has to read names the same way.
/// </summary>
internal static class GedcomName
{
  public static (string? Given, string? Surname) Parts(GedcomNode nameNode)
  {
    var given = nameNode.ChildValue(GedcomTags.Given) ?? GivenFromValue(nameNode.Value);
    var surname = nameNode.ChildValue(GedcomTags.Surname) ?? SurnameFromValue(nameNode.Value);
    return (given, surname);
  }

  private static string? GivenFromValue(string? value)
  {
    if (string.IsNullOrEmpty(value))
      return null;

    var slash = value.IndexOf('/');
    return (slash < 0 ? value : value[..slash]).Trim();
  }

  private static string? SurnameFromValue(string? value)
  {
    if (string.IsNullOrEmpty(value))
      return null;

    var open = value.IndexOf('/');
    if (open < 0)
      return null;

    var close = value.IndexOf('/', open + 1);
    return close < 0 ? null : value[(open + 1)..close].Trim();
  }
}
