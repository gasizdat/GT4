using GT4.Core.Project.Dto;

namespace GT4.Core.Gedcom;

/// <summary>Shared mappings between GEDCOM tokens and GT4 enumerations used on both the read and write paths.</summary>
internal static class GedcomMapping
{
  /// <summary>
  /// The owned INDI tags GT4 reads only part of, mapped to the sub-tags it models. Every other child of
  /// such a tag is unmodeled: import preserves it as residue and export merges it back into the regenerated
  /// node, so a round-trip keeps the data GT4 has no schema for (a BIRT's PLAC/SOUR, a NAME's NICK, ...).
  /// </summary>
  public static readonly IReadOnlyDictionary<string, HashSet<string>> OwnedTagModeledChildren = new Dictionary<string, HashSet<string>>
  {
    [GedcomTags.Name] = [GedcomTags.Given, GedcomTags.Surname],
    [GedcomTags.Birth] = [GedcomTags.Date],
    [GedcomTags.Death] = [GedcomTags.Date],
    [GedcomTags.Sex] = [],
    [GedcomTags.Note] = [],
  };

  /// <summary>
  /// Owned INDI tags regenerated wholesale from GT4's edge graph (with fresh xrefs and possibly several per
  /// person). Their unmodeled children cannot be merged back unambiguously on export, so they are neither
  /// preserved nor captured as residue. Their only common sub-tag, PEDI, is already modeled.
  /// </summary>
  public static readonly HashSet<string> FullyOwnedTags = [GedcomTags.FamilyChild, GedcomTags.FamilySpouse];

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
