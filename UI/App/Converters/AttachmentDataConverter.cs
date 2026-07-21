using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

/// <summary>
/// Converts a <see cref="DataCategory.PersonAttachment"/>'s <see cref="Data"/> to/from its display
/// filename. Unlike a photo, an attachment is never edited in place -- there is no UI that reassigns an
/// existing attachment's bytes -- so FromObjectAsync only ever builds a brand-new attachment from a
/// freshly picked file. Lives in UI.App (not UI.Utils) because encoding/decoding the residue envelope
/// needs Core.Gedcom, same reason as <see cref="PhotoTagDataConverter"/>.
/// </summary>
/// <summary>The object <see cref="AttachmentDataConverter.FromObjectAsync"/> expects: a freshly picked
/// file's bytes, its own file name, and the content type the picker reported (if any).</summary>
public sealed record AttachmentPick(byte[] Content, string FileName, string? MimeType);

public sealed class AttachmentDataConverter : IDataConverter
{
  private const string MimeTypeOctetStream = System.Net.Mime.MediaTypeNames.Application.Octet;

  public Task<Data?> FromObjectAsync(object? data, CancellationToken token)
  {
    // A null/empty MimeType would emit no FORM on export, which reimports as a photo instead of an
    // attachment -- so a picked file with no usable content type still gets a non-image, non-null one.
    Data? ret = data is AttachmentPick pick
      ? new Data(
          Id: ElementId.NonCommittedId,
          Content: GedcomPhotoResidue.EncodeAttachment(pick.Content, pick.FileName),
          MimeType: string.IsNullOrEmpty(pick.MimeType) ? MimeTypeOctetStream : pick.MimeType,
          Category: DataCategory.PersonAttachment)
      : null;

    return Task.FromResult(ret);
  }

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    return await GedcomPhotoResidue.ExtractFileNameAsync(data, token);
  }
}
