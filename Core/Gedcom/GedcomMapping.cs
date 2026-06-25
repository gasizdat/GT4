using GT4.Core.Project.Dto;

namespace GT4.Core.Gedcom;

/// <summary>Shared mappings between GEDCOM tokens and GT4 enumerations used on both the read and write paths.</summary>
internal static class GedcomMapping
{
  public static string SexLetter(BiologicalSex sex) => sex switch
  {
    BiologicalSex.Male => "M",
    BiologicalSex.Female => "F",
    _ => "U",
  };

  public static BiologicalSex ParseSex(string? letter) => letter?.Trim().ToUpperInvariant() switch
  {
    "M" => BiologicalSex.Male,
    "F" => BiologicalSex.Female,
    _ => BiologicalSex.Unknown,
  };

  /// <summary>The gendered declension flag GT4 stores alongside a name part, by the owner's sex.</summary>
  public static NameType Declension(BiologicalSex sex) => sex switch
  {
    BiologicalSex.Male => NameType.MaleDeclension,
    BiologicalSex.Female => NameType.FemaleDeclension,
    _ => 0,
  };
}
