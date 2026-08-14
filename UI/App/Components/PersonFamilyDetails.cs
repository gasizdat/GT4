using GT4.Core.Gedcom;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.UI.Converters;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Components;

public static class PersonFamilyDetails
{
  // Spelled out rather than taken from GedcomTags, which is internal to Core.Gedcom -- the same way
  // GedcomDataConverter names the tags it renders specially.
  private const string FamilyTag = "FAM";
  private const string MediaTag = "OBJE";

  /// <summary>
  /// A couple's preserved GEDCOM tags are keyed by the pair rather than carried on either person, so they
  /// are gathered from the spouse relationships instead of arriving on <see cref="PersonFullInfo"/> the way
  /// the person's own residue does. Several marriages to one spouse are one couple and one block.
  /// </summary>
  public static async Task<string?> ReadAsync(
    IProjectDocument project,
    PersonFullInfo person,
    AttachmentInfo[] attachments,
    INameFormatter nameFormatter,
    CancellationToken token)
  {
    var spouses = person.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse);
    var couples = spouses.GroupBy(spouse => spouse.Id).ToArray();
    Person[] spousePersons = [.. couples.Select(couple => couple.First())];
    var spouseAttachments = await project.PersonData.GetPersonDataSetAsync(spousePersons, DataCategory.PersonAttachment, token);
    var facts = new List<GedcomFact>();

    foreach (var couple in couples)
    {
      var spouse = couple.First();
      var name = nameFormatter.ToString(spouse, NameFormat.ShortPersonName);
      var dates = couple.Select(marriage => marriage.Date);
      var residue = await GedcomFamilyResidue.ReadAsync(project, person.Id, couple.Key, name, dates, token);
      var theirs = spouseAttachments.GetValueOrDefault(couple.Key, []);
      var media = await GedcomFamilyMedia.SelectSharedAsync(person.Attachments, theirs, token);
      GedcomFact[] children = [.. residue?.Children ?? [], .. media.Select(data => ToMediaFact(data, attachments))];
      if (children.Length == 0)
      {
        continue;
      }

      var spouseUrl = MarkdownLinkUtils.PersonUrl(couple.Key);
      facts.Add(new GedcomFact(FamilyTag, $"[{name}]({spouseUrl})", children));
    }

    return GedcomDataConverter.RenderFamilies([.. facts]);
  }

  private static GedcomFact ToMediaFact(Data media, AttachmentInfo[] attachments)
  {
    var info = attachments.First(attachment => attachment.Data.Id == media.Id);
    var url = MarkdownLinkUtils.AttachmentUrl(media.Id);
    return new GedcomFact(MediaTag, $"[{info.DisplayName}]({url})", []);
  }
}
