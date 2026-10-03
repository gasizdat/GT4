using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Utils;

public static class NoFamily
{
  // Sentinel family for persons that have no FamilyName-typed name; Id 0 never collides with a
  // real name because SQLite rowids start at 1.
  public static Name Name { get; } = new(0, UIStrings.FamilyNameNoFamily, NameType.FamilyName, null);

  public static bool Includes(PersonInfo person) =>
    !person.Names.Any(name => name.Type.HasFlag(NameType.FamilyName));
}
