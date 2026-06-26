using GT4.Core.Utils;
using System.Diagnostics;

namespace GT4.Core.Project.Dto;

[DebuggerDisplay("{BiologicalSex}, {DisplayName}")]
public record class PersonInfo(
  int Id,
  Date BirthDate,
  Date? DeathDate,
  BiologicalSex BiologicalSex,
  Name[] Names,
  Data? MainPhoto
) : Person(Id, BirthDate, DeathDate, BiologicalSex)
{
  public PersonInfo(Person person, Name[] names, Data? mainPhoto)
    : this(
        person.Id,
        person.BirthDate,
        person.DeathDate,
        person.BiologicalSex,
        names,
        mainPhoto
  )
  {
  }

  // The family name duplicates the last name's value (both are the surname), so it is left out of the
  // readable display name.
  public string DisplayName =>
    string.Join(" ", Names.Where(n => !n.Type.HasFlag(NameType.FamilyName)).Select(n => n.Value));
}