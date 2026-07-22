using System.Text;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;

namespace GT4.Core.Gedcom;

/// <summary>
/// Stores a photo's or attachment's own residual GEDCOM tags (a photo's chiefly <c>TITL</c>, the caption;
/// an attachment's <c>FILE</c>, the original filename) alongside its raw bytes, for photos whose
/// <see cref="DataCategory"/> is <see cref="DataCategory.PersonMainPhotoTagged"/>/
/// <see cref="DataCategory.PersonPhotoTagged"/>, and for every <see cref="DataCategory.PersonAttachment"/>.
/// Layout: <c>[4-byte tag-length][UTF-8 GedcomWriter-serialized residual node][raw bytes]</c>. There is no
/// magic-byte marker to sniff -- the category alone (<see cref="DataCategoryExtensions.IsTaggedPhoto"/> or
/// <see cref="DataCategoryExtensions.IsAttachment"/>) decides whether a <c>Content</c> uses this layout at
/// all, so the shape is never ambiguous.
/// </summary>
public static class GedcomPhotoResidue
{
  /// <summary>Builds a brand-new attachment's envelope from a freshly picked file -- there is no existing
  /// OBJE to carry residual tags from, so a <c>FILE</c> node holding the file's own name is synthesized.</summary>
  public static byte[] EncodeAttachment(byte[] fileBytes, string fileName)
  {
    var fileNode = new GedcomNode { Tag = GedcomTags.File, Value = fileName };
    var residual = new GedcomNode { Tag = GedcomTags.Object }.Add(fileNode);
    return Encode(fileBytes, residual);
  }

  internal static byte[] Encode(byte[] imageBytes, GedcomNode residual)
  {
    var tagWriter = new StringWriter();
    GedcomWriter.Write(tagWriter, residual);
    var tagBytes = Encoding.UTF8.GetBytes(tagWriter.ToString());

    using var buffer = new MemoryStream(sizeof(int) + tagBytes.Length + imageBytes.Length);
    buffer.Write(BitConverter.GetBytes(tagBytes.Length));
    buffer.Write(tagBytes);
    buffer.Write(imageBytes);
    return buffer.ToArray();
  }

  // Used by GedcomExporter, which needs the full node to re-attach its children onto a regenerated OBJE.
  internal static async Task<(byte[] ImageBytes, GedcomNode Residual)> DecodeAsync(byte[] content, CancellationToken token)
  {
    var tagLength = BitConverter.ToInt32(content, 0);
    var tagText = Encoding.UTF8.GetString(content, sizeof(int), tagLength);
    var imageBytes = content[(sizeof(int) + tagLength)..];

    var roots = await GedcomReader.ReadAsync(new StringReader(tagText), token);
    return (imageBytes, roots[0]);
  }

  /// <summary>Synchronous: just reads the length prefix and slices the remainder -- no GEDCOM parse at all.</summary>
  public static byte[] ExtractImageBytes(byte[] content)
  {
    var tagLength = BitConverter.ToInt32(content, 0);
    return content[(sizeof(int) + tagLength)..];
  }

  public static async Task<string?> ExtractTitleAsync(Data? photo, CancellationToken token)
  {
    if (photo is null || !photo.Category.IsTaggedPhoto())
      return null;

    var (_, residual) = await DecodeAsync(photo.Content, token);
    return residual.ChildValue(GedcomTags.Title);
  }

  public static async Task<string?> ExtractFileNameAsync(Data? attachment, CancellationToken token)
  {
    if (attachment is null || !attachment.Category.IsAttachment())
      return null;

    var (_, residual) = await DecodeAsync(attachment.Content, token);
    return residual.ChildValue(GedcomTags.File);
  }
}
