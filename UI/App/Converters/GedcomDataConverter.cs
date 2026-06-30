using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using GT4.UI.Utils.Converters;
using System.Globalization;
using System.Text;

namespace GT4.UI.Converters;

/// <summary>
/// Renders a person's preserved GEDCOM sub-tags (<see cref="DataCategory.PersonGedcomTags"/>) into a
/// markdown block shown alongside the biography. Each tag is labelled from <see cref="UIStrings"/> under the
/// <c>GedcomTag&lt;TAG&gt;</c> key, falling back to the raw GEDCOM tag, so common facts read naturally and
/// exotic ones still appear. Display only: <see cref="FromObjectAsync"/> yields nothing, since these tags
/// are not edited in the UI — they ride through unchanged on <see cref="PersonFullInfo.GedcomData"/>.
/// </summary>
public sealed class GedcomDataConverter : IDataConverter
{
  private const string LabelKeyPrefix = "GedcomTag";
  private const string MapTag = "MAP";
  private const string LatitudeTag = "LATI";
  private const string LongitudeTag = "LONG";

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token) =>
    throw new NotSupportedException("GEDCOM residue is display-only; it is carried verbatim, not edited in the UI.");

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    var facts = await GedcomNarrative.ParseAsync(data, token);
    return facts.Length == 0 ? null : Render(facts);
  }

  private static string Render(GedcomFact[] facts)
  {
    var builder = new StringBuilder();
    var title = Localized("FieldGedcomDetails", "Additional details");
    builder.Append("## ").Append(title).Append("\n\n");
    foreach (var fact in facts)
    {
      Append(builder, fact, 0);
    }
    return builder.ToString();
  }

  private static void Append(StringBuilder builder, GedcomFact fact, int depth)
  {
    var indent = new string(' ', depth * 2);

    // A MAP holding LATI/LONG renders as a single Google Maps link instead of bare coordinate bullets.
    if (fact.Tag == MapTag && TryFormatGeoLink(fact, out var geoLink))
    {
      builder.Append(indent).Append("- ").Append(Label(fact.Tag)).Append(": ").Append(geoLink).Append('\n');
      foreach (var child in fact.Children)
      {
        if (child.Tag != LatitudeTag && child.Tag != LongitudeTag)
          Append(builder, child, depth + 1);
      }
      return;
    }

    var label = Label(fact.Tag);
    var heading = depth == 0 ? $"**{label}**" : label;
    builder.Append(indent).Append("- ").Append(heading);
    if (!string.IsNullOrEmpty(fact.Value))
    {
      builder.Append(": ").Append(fact.Value);
    }
    builder.Append('\n');
    foreach (var child in fact.Children)
    {
      Append(builder, child, depth + 1);
    }
  }

  /// <summary>
  /// Turns a MAP fact's LATI/LONG into a markdown link to Google Maps, e.g.
  /// <c>[48.8979, 2.09592](https://www.google.com/maps?q=48.8979,2.09592)</c>. Returns false when either
  /// coordinate is missing or unparseable, so the MAP then renders as ordinary nested facts.
  /// </summary>
  private static bool TryFormatGeoLink(GedcomFact map, out string link)
  {
    link = string.Empty;
    var latitudeValue = map.Children.FirstOrDefault(c => c.Tag == LatitudeTag)?.Value;
    var longitudeValue = map.Children.FirstOrDefault(c => c.Tag == LongitudeTag)?.Value;
    var latitude = ParseCoordinate(latitudeValue, 'N', 'S');
    var longitude = ParseCoordinate(longitudeValue, 'E', 'W');
    if (latitude is null || longitude is null)
      return false;

    var latitudeText = latitude.Value.ToString(CultureInfo.InvariantCulture);
    var longitudeText = longitude.Value.ToString(CultureInfo.InvariantCulture);
    var url = $"https://www.google.com/maps?q={latitudeText},{longitudeText}";
    link = $"[{latitudeText}, {longitudeText}]({url})";
    return true;
  }

  /// <summary>
  /// Parses a GEDCOM 5.5.1 coordinate (a hemisphere letter then decimal degrees, e.g. <c>N48.8979</c>) into
  /// a signed decimal: <paramref name="positive"/> (N/E) keeps the sign, <paramref name="negative"/> (S/W)
  /// flips it. A value with no leading hemisphere letter is read as an already-signed decimal.
  /// </summary>
  private static double? ParseCoordinate(string? value, char positive, char negative)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    var text = value.Trim();
    var hemisphere = char.ToUpperInvariant(text[0]);
    var sign = hemisphere == positive ? 1 : hemisphere == negative ? -1 : 0;
    var number = sign == 0 ? text : text[1..];
    if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
      return null;

    return sign == 0 ? degrees : sign * degrees;
  }

  // GEDCOM tags are open-ended, so labels are looked up dynamically (no generated property per tag) and
  // fall back to the raw tag when there is no localized string for it.
  private static string Label(string tag) => Localized(LabelKeyPrefix + tag, tag);

  private static string Localized(string key, string fallback)
  {
    var value = UIStrings.ResourceManager.GetString(key, UIStrings.Culture);
    return value ?? fallback;
  }
}
