using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using System.Diagnostics.CodeAnalysis;

namespace GT4.UI.Utils.Formatters.Detailed;

using Converters = Func<string>[][];
using Row = RelationshipTypeTableRow;
using Table = Dictionary<RelationshipType, RelationshipTypeTableRow>;

internal abstract class RelationshipTypeFormatterBase
{
  private static readonly Generation _GreatnessStartLevel = new Generation(2);
  private static readonly Generation _GreatnessMaxLevel = new Generation(4);

  private readonly RelationshipType _Type;
  private readonly BiologicalSex _Sex;
  private readonly Generation _Gen;
  private readonly Generation _AbsGen;
  private readonly Consanguinity _Con;
  private readonly Converters _Converters;
  private static bool? _IsRunningInTest;

  protected Generation GreatnessStartLevel => _GreatnessStartLevel;
  protected RelationshipType Type => _Type;
  protected BiologicalSex Sex => _Sex;
  protected Generation Gen => _Gen;
  protected Generation AbsGen => _AbsGen;
  protected Consanguinity Con => _Con;

  protected RelationshipTypeFormatterBase(RelationshipType type, BiologicalSex? biologicalSex, Generation? generation, Consanguinity? consanguinity)
  {
    _Type = type;
    _Sex = biologicalSex ?? BiologicalSex.Unknown;
    _Gen = generation ?? Generation.Zero;
    _Con = consanguinity ?? Consanguinity.Zero;
    _AbsGen = Gen < Generation.Zero ? Generation.Zero - Gen : Gen;
    _Converters = GetConverters();
  }

  public override string ToString()
  {
    try
    {
      var toString = GetConverter();
      var ret = toString();
      ret = Normalize(ret);

#if DEBUG
      if (!IsRunningInTest)
      {
        ret = $"{ret} G{Gen.Value} C{Con.Value}";
      }
#endif

      return ret;
    }
    catch (Exception ex)
    {
      return ex.Message;
    }
  }

  protected abstract Converters GetConverters();

  /// <summary>
  /// En/Ru tables and templates are written in sentence case, so the only capitals to remove are
  /// the ones composition introduced ("Great-" + "Grandparent").
  /// </summary>
  protected virtual string Normalize(string composed) => composed.Substring(0, 1) + composed.Substring(1).ToLower();

  /// <summary>
  /// Builds a "Great-grandparent"-style label by wrapping <paramref name="main"/> in
  /// <see cref="UIStrings.RelGreat_1"/> once per generation, falling back to a "N-Great-..." numeral
  /// form past <see cref="_GreatnessMaxLevel"/>. Repeating one prefix around the whole composed
  /// phrase is an En/Ru convention rather than a universal one: Spanish names each level with its
  /// own prefix bound to the kinship stem, so <see cref="RelationshipTypeFormatterEs"/> overrides
  /// this instead of reusing it. Confirm a new language really does repeat one prefix before
  /// inheriting this.
  /// <para/>
  /// When <paramref name="main"/> spells out both <paramref name="femaleStem"/> and
  /// <paramref name="maleStem"/> in full (a disjunction like "Großonkel oder Großtante"), each stem
  /// is depth-marked on its own so both sides gain the prefix instead of only the first (#318). That
  /// includes the numeral used past <see cref="_GreatnessMaxLevel"/>: the prefix itself carries no
  /// count ("Ur-"/"arrière-"/"Пра-" mean "one step further" at any depth), so the numeral is the only
  /// place the depth is recorded and a disjunct without it reads as the shallow term. English elides
  /// the second stem ("Grand uncle or aunt"), so the stems there never match in full and the phrase
  /// is wrapped as one unit, unaffected.
  /// </summary>
  protected virtual string AddGreatness(string main, string femaleStem = "", string maleStem = "")
  {
    var generation = AbsGen - GreatnessStartLevel;
    var wraps = generation > _GreatnessMaxLevel ? 1 : Math.Max(generation.Value, 0);

    string Depth(string stem)
    {
      var wrapped = stem;
      for (var i = 0; i < wraps; i++)
      {
        wrapped = string.Format(UIStrings.RelGreat_1, wrapped);
      }
      return generation > _GreatnessMaxLevel ? $"{generation.Value}-{wrapped}" : wrapped;
    }

    var maleIndex = maleStem.Length > 0 ? main.IndexOf(maleStem, StringComparison.OrdinalIgnoreCase) : -1;
    var femaleIndex = maleIndex >= 0 && femaleStem.Length > 0
      ? main.IndexOf(femaleStem, maleIndex + maleStem.Length, StringComparison.OrdinalIgnoreCase)
      : -1;

    var ret = femaleIndex > maleIndex
      ? main.Substring(0, maleIndex)
        + Depth(maleStem)
        + main.Substring(maleIndex + maleStem.Length, femaleIndex - maleIndex - maleStem.Length)
        + Depth(femaleStem)
        + main.Substring(femaleIndex + femaleStem.Length)
      : Depth(main);

    return ret;
  }

  public static bool IsRunningInTest
  {
    get
    {
      if (!_IsRunningInTest.HasValue)
      {
        string[] testHosts = ["xunit.runner", "xunit.v3.runner", "nunit.framework", "Microsoft.VisualStudio.TestPlatform"];
        _IsRunningInTest = AppDomain
          .CurrentDomain
          .GetAssemblies()
          .Any(a => testHosts.Any(b => a?.FullName?.StartsWith(b) == true));
      }
      return _IsRunningInTest.Value;
    }
  }

  [DoesNotReturn]
  protected string Guard()
  {
    throw new ArgumentException($"Unsupported or wrong relationship: Type={Type}, Sex={Sex}, G{Gen.Value}, C{Con.Value}");
  }

  protected Func<string> GetConverter()
  {
    var gen = AbsGen.Value;
    var candidates = _Converters.Length > gen ? _Converters[gen] : _Converters.Last();
    var ret = candidates.Length > Con.Value ? candidates[Con.Value] : candidates.Last();

    return ret;
  }

  protected string ToString(Table table, RelationshipType? type = null) => ToString(table, type, out _);

  protected string ToString(Table table, out Row leaf) => ToString(table, null, out leaf);

  /// <summary>
  /// Also hands back the leaf row whose F/M actually produced <c>ret</c>, even when <paramref
  /// name="type"/> only redirects (<see cref="RelationshipTypeTableRow.SubType"/>) or composes
  /// (e.g. "Adoptive {0}") into it. <see cref="AddGreatness(string, string, string)"/> needs those
  /// stems to prefix a disjunction on both sides.
  /// </summary>
  protected string ToString(Table table, RelationshipType? type, out Row leaf)
  {
    if (!table.TryGetValue(type ?? Type, out var row))
    {
      Guard();
    }

    var ret = row.ToString(Sex);
    if (ret == string.Empty)
    {
      if (!row.SubType.HasValue)
      {
        Guard();
      }
      ret = ToString(table, row.SubType.Value, out leaf);
    }
    else if (row.SubType.HasValue)
    {
      ret = string.Format(ret, ToString(table, row.SubType.Value, out leaf));
    }
    else
    {
      leaf = row;
    }

    return ret;
  }
}
