using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using System.Diagnostics.CodeAnalysis;

namespace GT4.UI.Utils.Formatters.Detailed;

using Converters = Func<string>[][];
using Table = Dictionary<RelationshipType, RelationshipTypeTableRow>;

internal abstract class RelationshipTypeFormatterBase
{
  private readonly RelationshipType _Type;
  private readonly BiologicalSex _Sex;
  private readonly Generation _Gen;
  private readonly Generation _AbsGen;
  private readonly Consanguinity _Con;
  private readonly Converters _Converters;

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
      ret = ret.Substring(0, 1) + ret.Substring(1).ToLower();
#if DEBUG
      return $"{ret} G{Gen.Value}, C{Con.Value}";
#else
      return ret;
#endif
    }
    catch (Exception ex)
    {
      return ex.Message;
    }
  }

  protected abstract Converters GetConverters();


  [DoesNotReturn]
  protected string Guard()
  {
    throw new ArgumentException($"Unsupported or wrong relationship: Type={Type}, Sex={Sex}, G{Gen.Value}, C{Con.Value}");
  }

  protected Func<string> GetConverter()
  {
    var gen = Math.Abs(Gen.Value);
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
