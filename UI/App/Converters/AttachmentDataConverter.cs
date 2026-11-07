using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Converters;

public sealed record AttachmentPick(byte[] Content, string FileName, string? MimeType);

/// <summary>
/// Converts a <see cref="DataCategory.PersonAttachment"/>/<see cref="DataCategory.FamilyAttachment"/>'s
/// <see cref="Data"/> to/from an <see cref="AttachmentInfo"/>; the constructor's category picks which of
/// the two a new attachment is stamped as (one keyed DI registration per category). Unlike a photo,
/// an attachment is never edited in place -- there is no UI that reassigns an existing attachment's bytes
/// -- so FromObjectAsync only ever builds a brand-new attachment: from a freshly picked file, or from an
/// <see cref="AttachmentInfo"/> whose Image is an enhanced copy of its source's. Lives in UI.App (not
/// UI.Utils) because encoding/decoding the residue envelope needs Core.Gedcom, same reason as
/// <see cref="PhotoTagDataConverter"/>.
/// </summary>
public sealed class AttachmentDataConverter : IDataConverter
{
  private const string MimeTypeOctetStream = System.Net.Mime.MediaTypeNames.Application.Octet;

  private readonly DataCategory _Category;
  private readonly ImageDataConverter _ImageConverter;

  public AttachmentDataConverter([ServiceKey] DataCategory category, IHttpClientFactory httpClientFactory, IImageCache imageCache)
  {
    _Category = category;
    _ImageConverter = new ImageDataConverter(httpClientFactory, imageCache);
  }

  public async Task<Data?> FromObjectAsync(object? data, CancellationToken token) => data switch
  {
    AttachmentPick pick => FromFile(pick.Content, pick.FileName, pick.MimeType),
    AttachmentInfo { Image: { } image } attachment => await FromImageAsync(attachment, image, token),
    _ => null
  };

  public async Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    if (data is null)
    {
      return null;
    }

    var fileName = await GedcomPhotoResidue.ExtractFileNameAsync(data, token);
    var title = await GedcomPhotoResidue.ExtractTitleAsync(data, token);
    var image = data.IsInlineImage()
      ? await ToImageAsync(data, token)
      : null;

    return new AttachmentInfo(fileName ?? string.Empty, title, data, image);
  }

  // Null/empty MimeType emits no FORM on export and reimports as a photo, not an attachment.
  private Data FromFile(byte[] content, string fileName, string? mimeType) => new(
    Id: ElementId.NonCommittedId,
    Content: GedcomPhotoResidue.EncodeAttachment(content, fileName),
    MimeType: string.IsNullOrEmpty(mimeType) ? MimeTypeOctetStream : mimeType,
    Category: _Category);

  private async Task<Data> FromImageAsync(AttachmentInfo attachment, PhotoInfo image, CancellationToken token)
  {
    var encoded = await _ImageConverter.FromObjectAsync(image, token);
    var form = GedcomMedia.SniffForm(encoded!.Content);
    var fileName = Path.ChangeExtension(attachment.FileName, form);
    var mimeType = GedcomMedia.ToMimeType(form);
    var copy = FromFile(encoded.Content, fileName, mimeType);
    return await GedcomPhotoResidue.WithTitleAsync(copy, attachment.Title, token);
  }

  private async Task<PhotoInfo?> ToImageAsync(Data data, CancellationToken token)
  {
    var unwrapped = data with { Content = GedcomPhotoResidue.ExtractImageBytes(data.Content) };
    return await _ImageConverter.ToObjectAsync(unwrapped, token) as PhotoInfo;
  }
}
