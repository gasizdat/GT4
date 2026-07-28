using GT4.Core.Project.Dto;

namespace GT4.Core.Gedcom;

/// <summary>
/// The media a FAM record carried, recovered from a person's side of the marriage. GT4 has no family entity
/// to hold it, so import commits each family OBJE once and links that single row to both spouses -- an
/// attachment row a person shares with a spouse is therefore exactly the media their family record carried.
/// Media owned only by pointer (<see cref="GedcomTags.ReferencedRecord"/>) is excluded: its row is shared by
/// everyone citing that source, which says nothing about the couple.
/// </summary>
public static class GedcomFamilyMedia
{
  public static async Task<Data[]> SelectSharedAsync(
    IEnumerable<Data> attachments,
    IEnumerable<Data> spouseAttachments,
    CancellationToken token)
  {
    var spouseIds = spouseAttachments.Select(data => data.Id).ToHashSet();
    var shared = new List<Data>();

    foreach (var attachment in attachments.Where(data => spouseIds.Contains(data.Id)))
    {
      var (_, residual) = await GedcomPhotoResidue.DecodeAsync(attachment.Content, token);
      if (residual.Child(GedcomTags.ReferencedRecord) is null)
      {
        shared.Add(attachment);
      }
    }

    return [.. shared];
  }
}
