using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Utils.Formatters.Detailed;

using S = UIStrings;

/// <summary>
/// Spanish composes kinship over the same generation grid as English and shares its sentence
/// casing, so it derives from <see cref="RelationshipTypeFormatterEn"/> and only replaces the way
/// generation depth is named.
/// </summary>
internal class RelationshipTypeFormatterEs : RelationshipTypeFormatterEn
{
  private static readonly Generation _GreatnessStartLevel = new Generation(2);
  private static readonly Generation _NamedPrefixMaxLevel = new Generation(2);

  public RelationshipTypeFormatterEs(RelationshipType type, BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
    : base(type, biologicalSex, generation, consanguinity)
  {
  }

  /// <summary>
  /// Spanish binds the generation prefix to the kinship stem rather than to the phrase around it,
  /// so the stem is what gets rewritten: "tío abuelo" -> "tío bisabuelo", "abuelo o abuela" ->
  /// "bisabuelo o bisabuela", and the trailing adjective of "abuelo adoptivo" stays put. Every
  /// composed term reaching here must therefore spell one of the four stems.
  /// </summary>
  protected override string AddGreatness(string main)
  {
    var depth = AbsGen - _GreatnessStartLevel;

    if (depth <= Generation.Zero)
    {
      return main;
    }

    var prefix = depth < _NamedPrefixMaxLevel ? S.RelGreat_1 : S.RelGreat2_es_1;
    string[] stems = [S.RelGrandFather, S.RelGrandMother, S.RelGrandSon, S.RelGrandDaughter];
    var ret = main;

    foreach (var stem in stems)
    {
      var prefixed = Prefix(prefix, stem);
      ret = ret.Replace(stem, prefixed, StringComparison.OrdinalIgnoreCase);
    }

    if (depth > _NamedPrefixMaxLevel)
    {
      ret = $"{depth.Value}-{ret}";
    }

    return ret;
  }

  /// <summary>
  /// "tatara" and "abuelo" contract to "tatarabuelo" while "tatara" and "nieto" simply run together,
  /// so the seam loses a letter exactly when the prefix ends with the one the stem opens with.
  /// </summary>
  private static string Prefix(string template, string stem)
  {
    var seam = template.IndexOf("{0}", StringComparison.Ordinal);
    var contracts = char.ToLower(template[seam - 1]) == char.ToLower(stem[0]);
    var tail = contracts ? stem.Substring(1) : stem;
    var ret = string.Format(template, tail);

    return ret;
  }
}
