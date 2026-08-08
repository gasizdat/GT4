using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

public sealed record AttachmentPick(byte[] Content, string FileName, string? MimeType);

/// <summary>
/// Converts a <see cref="DataCategory.PersonAttachment"/>/<see cref="DataCategory.FamilyAttachment"/>'s
/// <see cref="Data"/> to/from its display filename; the constructor's category picks which of the two a
/// freshly picked file is stamped as (one keyed DI registration per category). Unlike a photo, an
/// attachment is never edited in place -- there is no UI that reassigns an existing attachment's bytes --
/// so FromObjectAsync only ever builds a brand-new attachment from a freshly picked file. Lives in UI.App
/// (not UI.Utils) because encoding/decoding the residue envelope needs Core.Gedcom, same reason as
/// <see cref="PhotoTagDataConverter"/>.
/// </summary>
public sealed class AttachmentDataConverter : IDataConverter
{
  private const string MimeTypeOctetStream = System.Net.Mime.MediaTypeNames.Application.Octet;

  private readonly DataCategory _Category;

  public AttachmentDataConverter(DataCategory category) => _Category = category;

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token)
  {
    // Null/empty MimeType emits no FORM on export and reimports as a photo, not an attachment.
    Data? ret = data is AttachmentPick pick
      ? new Data(
          Id: ElementId.NonCommittedId,
          Content: GedcomPhotoResidue.EncodeAttachment(pick.Content, pick.FileName),
          MimeType: string.IsNullOrEmpty(pick.MimeType) ? MimeTypeOctetStream : pick.MimeType,
          Category: _Category)
      : null;

    return Task.FromResult(ret);
  }

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token) => await GedcomPhotoResidue.ExtractFileNameAsync(data, token);
}
