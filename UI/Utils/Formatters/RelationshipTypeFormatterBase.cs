using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using System.Diagnostics.CodeAnalysis;

namespace GT4.UI.Utils.Formatters.Detailed;

using Converters = Func<string>[][];
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
  private bool _NeutralTermMissing;
  private static bool? _IsRunningInTest;

  /// <summary>
  /// Whether the terms this language spells for the relationship just formatted include no
  /// unknown-sex one of its own, leaving the result of <see cref="ToString"/> to be replaced by a
  /// <see cref="Join"/> of the two gendered labels. Only meaningful once <see cref="ToString"/> has
  /// run, since the tables are what name the terms.
  /// </summary>
  public bool NeutralTermMissing => _NeutralTermMissing;

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

      return Normalize(ret);
    }
    catch (Exception ex)
    {
      return ex.Message;
    }
  }

  /// <summary>
  /// Joins the two gendered labels for a language with no unknown-sex term of its own. Both are
  /// already complete, so every gendered part - the generation prefix, the consanguinity adjective -
  /// is present on each side and agrees with it. A table's unknown-sex term is taken to be one of
  /// those languages exactly when it reproduces this join, since then it says nothing the two
  /// gendered terms do not; a distinct word ("Großelternteil") or a shortened one ("Grand uncle or
  /// aunt") does, and is kept.
  /// </summary>
  public string Join(string male, string female)
  {
    var joined = string.Format(UIStrings.RelDisjunction_2, male, female);

    return Normalize(joined);
  }

  protected abstract Converters GetConverters();

  /// <summary>
  /// En/Ru tables and templates are written in sentence case, so the only capitals to remove are
  /// the ones composition introduced ("Great-" + "Grandparent").
  /// </summary>
  protected virtual string Normalize(string composed) => composed.Substring(0, 1) + composed.Substring(1).ToLower();

  /// <summary>
  /// Builds a "Great-grandparent"-style label by recursively wrapping <paramref name="main"/> in
  /// <see cref="UIStrings.RelGreat_1"/>, falling back to a "N-Great-..." numeral form past
  /// <see cref="_GreatnessMaxLevel"/>. Repeating one prefix around the whole composed phrase is an
  /// En/Ru convention rather than a universal one: Spanish names each level with its own prefix
  /// bound to the kinship stem, so <see cref="RelationshipTypeFormatterEs"/> overrides this instead
  /// of reusing it. Confirm a new language really does repeat one prefix before inheriting this.
  /// </summary>
  protected virtual string AddGreatness(string main)
  {
    var ret = main;
    var generation = AbsGen - GreatnessStartLevel;

    if (generation > _GreatnessMaxLevel)
    {
      ret = $"{generation.Value}-{string.Format(UIStrings.RelGreat_1, ret)}";
    }
    else
    {
      while (generation-- > Generation.Zero)
      {
        ret = string.Format(UIStrings.RelGreat_1, ret);
      }
    }
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

  protected string ToString(Table table, RelationshipType? type = null)
  {
    if (!table.TryGetValue(type ?? Type, out var row))
    {
      Guard();
    }

    var ret = row.ToString(Sex);
    _NeutralTermMissing |= Sex == BiologicalSex.Unknown && ret == Join(row.M, row.F);

    if (ret == string.Empty)
    {
      if (!row.SubType.HasValue)
      {
        Guard();
      }
      ret = ToString(table, row.SubType.Value);
    }
    else if (row.SubType.HasValue)
    {
      ret = string.Format(ret, ToString(table, row.SubType.Value));
    }

    return ret;
  }
}
