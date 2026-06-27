using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using GT4.UI.Utils.Converters;
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

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token) =>
    throw new NotSupportedException("GEDCOM residue is display-only; it is carried verbatim, not edited in the UI.");

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    var facts = await GedcomNarrative.ParseAsync(data, token);
    object? markdown = facts.Length == 0 ? null : Render(facts);
    return Task.FromResult(markdown);
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

  // GEDCOM tags are open-ended, so labels are looked up dynamically (no generated property per tag) and
  // fall back to the raw tag when there is no localized string for it.
  private static string Label(string tag) => Localized(LabelKeyPrefix + tag, tag);

  private static string Localized(string key, string fallback)
  {
    var value = UIStrings.ResourceManager.GetString(key, UIStrings.Culture);
    return value ?? fallback;
  }
}
